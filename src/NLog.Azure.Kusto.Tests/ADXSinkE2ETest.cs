using Kusto.Cloud.Platform.Utils;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using NLog.Azure.Kusto;
using NLog.Config;
using System.Data;
using Xunit;

namespace NLog.Azure.Kusto.Tests
{
    public class ADXSinkE2ETest : IDisposable
    {
        private readonly string m_generatedTableName = $"ADXNlogSink_{new Random().Next()}";
        private readonly KustoConnectionStringBuilder m_kustoConnectionStringBuilder;
        private readonly KustoConnectionStringBuilder m_kustoConnectionStringBuilderDM;

        public ADXSinkE2ETest()
        {
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new ArgumentNullException("CONNECTION_STRING not set");
            var database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set");

            KustoConnectionStringBuilder.DefaultPreventAccessToLocalSecretsViaKeywords = false;
            string dmConnectionStringEndpoint = connectionString.Contains("ingest-") ? connectionString : connectionString.ReplaceFirstOccurrence("://", "://ingest-");
            string engineConnectionStringEndpoint = !connectionString.Contains("ingest-") ? connectionString : connectionString.ReplaceFirstOccurrence("ingest-", "");

            m_kustoConnectionStringBuilder = new KustoConnectionStringBuilder(engineConnectionStringEndpoint);
            m_kustoConnectionStringBuilder.UserNameForTracing = "NLogE2ETest";
            m_kustoConnectionStringBuilderDM = new KustoConnectionStringBuilder(dmConnectionStringEndpoint);
            m_kustoConnectionStringBuilderDM.UserNameForTracing = "NLogE2ETest";

            var createTableCommand = CslCommandGenerator.GenerateTableCreateCommand(m_generatedTableName,
            new[]
            {
                Tuple.Create("Timestamp", "System.DateTime"),
                Tuple.Create("Level", "System.String"),
                Tuple.Create("Message", "System.string"),
                Tuple.Create("FormattedMessage", "System.string"),
                Tuple.Create("Exception", "System.string"),
                Tuple.Create("Properties", "System.Object"),
            });

            var alterBatchingPolicy = CslCommandGenerator.GenerateTableAlterIngestionBatchingPolicyCommand(
                database,
                m_generatedTableName,
                new IngestionBatchingPolicy(TimeSpan.FromSeconds(10), 3, 1024));

            var enableStreamingIngestion = CslCommandGenerator.GenerateTableAlterStreamingIngestionPolicyCommand(
                m_generatedTableName,
                true);

            var refreshDmPolicies = CslCommandGenerator.GenerateDmRefreshPoliciesCommand();

            WithTimeout("Setup Kusto", TimeSpan.FromSeconds(180), Task.Run(async () =>
            {
                using ICslAdminProvider kustoClient = KustoClientFactory.CreateCslAdminProvider(m_kustoConnectionStringBuilder);
                using ICslAdminProvider kustoClientDM = KustoClientFactory.CreateCslAdminProvider(m_kustoConnectionStringBuilderDM);

                await WithTimeout("Create Kusto Tables", TimeSpan.FromSeconds(120), Task.Run(() =>
                {
                    kustoClient.ExecuteControlCommand(database, createTableCommand);
                }));
                await WithTimeout("Alter Kusto Batching", TimeSpan.FromSeconds(120), Task.Run(() =>
                {
                    kustoClient.ExecuteControlCommand(database, alterBatchingPolicy);
                }));
                await WithTimeout("Alter Kusto Streaming", TimeSpan.FromSeconds(120), Task.Run(() =>
                {
                    kustoClient.ExecuteControlCommand(database, enableStreamingIngestion);
                }));
                await WithTimeout("Create Kusto-DM Tables ", TimeSpan.FromSeconds(120), Task.Run(() =>
                {
                    kustoClientDM.ExecuteControlCommand(database, ".refresh database '" + database + "' table '" + m_generatedTableName + "' cache ingestionbatchingpolicy");
                }));
            })).Wait();
        }

        private static async Task WithTimeout(string operationName, TimeSpan timeout, Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) != task)
                throw new TimeoutException(operationName);
        }

        [Theory]
        [InlineData("Test_ADXTargetStreamed", 10, 12, 5)]
        [InlineData("Test_ADXNTargetBatched", 10, 12, 5)]
        public async Task Test_LogMessage(string testType, int numberOfLogs, int retries, int delayTimeSecs)
        {
            Logger? logger = null;

            var stringWriter = new StringWriter();
            NLog.Common.InternalLogger.LogWriter = stringWriter;
            NLog.Common.InternalLogger.LogLevel = LogLevel.Warn;

            try
            {
                await WithTimeout("Create Kusto Logger", TimeSpan.FromSeconds(30), Task.Run(() =>
                {
                    logger = GetCustomLogger(testType);
                }));

                if (logger == null) throw new Exception("Logger/Test type not supported");

                for (int i = 0; i < numberOfLogs; i++)
                {
                    logger.Info("{type} Processed Info log {i}", testType, i);
                    logger.Debug("{type} Processed debug Log {i}", testType, i);
                    logger.Error(new Exception("{" + testType + "} : This is E2E Exception."));
                }

                await WithTimeout("Verify Kusto Logger", TimeSpan.FromSeconds(retries * delayTimeSecs + 120), Task.Run(async () =>
                {
                    using (var kustoClient = KustoClientFactory.CreateCslQueryProvider(m_kustoConnectionStringBuilder))
                    {
                        var finalExpCount = 0L;
                        var count = 0L;
                        for (int i = 0; i < retries; i++)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(delayTimeSecs));

                            using var reader = kustoClient.ExecuteQuery(m_generatedTableName + " | where FormattedMessage contains \"" + testType + "\" | count; " +
                                m_generatedTableName + " | where FormattedMessage contains \"" + testType + "\" and  not(isempty(Exception))  | count");
                            while (reader.Read())
                            {
                                count = reader.GetInt64(0);
                            }
                            reader.NextResult();
                            while (reader.Read())
                            {
                                finalExpCount = reader.GetInt64(0);
                            }
                            if (finalExpCount == numberOfLogs && count == (3 * numberOfLogs))
                            {
                                break;
                            }
                        }
                        Assert.Equal(3 * numberOfLogs, count);
                        Assert.Equal(numberOfLogs, finalExpCount);
                    }
                }));
            }
            catch (Exception exception)
            {
                var nlogOutput = stringWriter.ToString();
                if (string.IsNullOrEmpty(nlogOutput))
                    throw;
                throw new Exception(nlogOutput, exception);
            }
            finally
            {
                NLog.Common.InternalLogger.LogWriter = null;
            }
        }

        private Logger GetCustomLogger(string type)
        {
            switch (type)
            {
                case "Test_ADXNTargetBatched":
                    {
                        var target = new ADXTarget
                        {
                            Name = "adxtarget",
                            ConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new ArgumentNullException("CONNECTION_STRING not set"),
                            Database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set"),
                            TableName = m_generatedTableName,
                            UseStreamingIngestion = "false",
                            FlushImmediately = "true"
                        };
                        var config = new LoggingConfiguration();
                        config.AddRuleForAllLevels(target);
                        LogManager.Configuration = config;
                        return LogManager.GetLogger(type);
                    }
                case "Test_ADXTargetStreamed":
                    {
                        var target = new ADXTarget
                        {
                            Name = "adxtarget",
                            ConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new ArgumentNullException("CONNECTION_STRING not set"),
                            Database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set"),
                            TableName = m_generatedTableName,
                            UseStreamingIngestion = "true"
                        };
                        var config = new LoggingConfiguration();
                        config.AddRuleForAllLevels(target);
                        LogManager.Configuration = config;
                        return LogManager.GetLogger(type);
                    }

            }
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public void Dispose()
        {
            WithTimeout("Dispose Kusto", TimeSpan.FromSeconds(120), Task.Run(() =>
            {
                using (var queryProvider = KustoClientFactory.CreateCslAdminProvider(m_kustoConnectionStringBuilder))
                {
                    var command = CslCommandGenerator.GenerateTableDropCommand(m_generatedTableName);
                    var clientRequestProperties = new ClientRequestProperties()
                    {
                        ClientRequestId = Guid.NewGuid().ToString()
                    };
                    queryProvider.ExecuteControlCommand(Environment.GetEnvironmentVariable("DATABASE"), command,
                        clientRequestProperties);
                }
            })).Wait();
        }
    }
}
