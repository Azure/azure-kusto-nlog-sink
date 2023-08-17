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
        private readonly string? m_generatedTableName;
        private readonly KustoConnectionStringBuilder? m_kustoConnectionStringBuilder;
        private readonly KustoConnectionStringBuilder? m_kustoConnectionStringBuilderDM;

        public ADXSinkE2ETest()
        {
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new ArgumentNullException("CONNECTION_STRING not set");
            var database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set");

            KustoConnectionStringBuilder.DefaultPreventAccessToLocalSecretsViaKeywords = false;
            string dmConnectionStringEndpoint = connectionString.Contains("ingest-") ? connectionString : connectionString.ReplaceFirstOccurrence("://", "://ingest-");
            string engineConnectionStringEndpoint = !connectionString.Contains("ingest-") ? connectionString : connectionString.ReplaceFirstOccurrence("ingest-", "");

            m_kustoConnectionStringBuilder = new KustoConnectionStringBuilder(engineConnectionStringEndpoint);
            m_kustoConnectionStringBuilderDM = new KustoConnectionStringBuilder(dmConnectionStringEndpoint);

            using ICslAdminProvider kustoClient = KustoClientFactory.CreateCslAdminProvider(m_kustoConnectionStringBuilder), kustoClientDM = KustoClientFactory.CreateCslAdminProvider(m_kustoConnectionStringBuilderDM);

            var randomInt = new Random().Next();
            m_generatedTableName = "ADXNlogSink_" + randomInt;
            
            var command = CslCommandGenerator.GenerateTableCreateCommand(m_generatedTableName,
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
                new IngestionBatchingPolicy(TimeSpan.FromSeconds(1), 3, 1024));

            var enableStreamingIngestion = CslCommandGenerator.GenerateTableAlterStreamingIngestionPolicyCommand(
                m_generatedTableName,
                true);

            var refreshDmPolicies = CslCommandGenerator.GenerateDmRefreshPoliciesCommand();

            kustoClient.ExecuteControlCommand(database, command);
            kustoClient.ExecuteControlCommand(database, alterBatchingPolicy);
            kustoClient.ExecuteControlCommand(database, enableStreamingIngestion);
                
            kustoClientDM.ExecuteControlCommand(database, ".refresh database '" + database + "' table '" + m_generatedTableName + "' cache ingestionbatchingpolicy");
        }


        [Theory]
        [InlineData("Test_ADXTargetStreamed", 10, 12, 30000)]
        [InlineData("Test_ADXNTargetBatched", 10, 12, 30000)]
        public async void Test_LogMessage(string testType, int numberOfLogs, int retries, int delayTime)
        {
            Logger logger = GetCustomLogger(testType);
            if (logger == null) throw new Exception("Logger/Test type not supported");
            for (int i = 0; i < numberOfLogs; i++)
            {
                logger.Info("{type} Processed Info log {i}", testType, i);
                logger.Debug("{type} Processed debug Log {i}", testType, i);
                logger.Error(new Exception("{" + testType + "} : This is E2E Exception."));
            }

            using (var kustoClient = KustoClientFactory.CreateCslQueryProvider(m_kustoConnectionStringBuilder))
            {
                var finalExpCount = 0L;
                var count = 0L;
                IDataReader reader;
                for (int i = 0; i < retries; i++)
                {
                    await Task.Delay(delayTime);
                    reader = kustoClient.ExecuteQuery(m_generatedTableName + " | where FormattedMessage contains \"" + testType + "\" | count; " +
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
        }

        private Logger GetCustomLogger(string type)
        {
            switch (type)
            {
                case "Test_ADXNTargetBatched":
                    {
                        var target = new ADXTarget
                        {
                            ConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new ArgumentNullException("CONNECTION_STRING not set"),
                            Database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set"),
                            TableName = m_generatedTableName,
                            UseStreamingIngestion = "false",
                            FlushImmediately = "true"
                        };
                        var config = new LoggingConfiguration();
                        config.AddTarget("adxtarget", target);
                        var rule = new LoggingRule("*", LogLevel.Debug, target);
                        config.LoggingRules.Add(rule);
                        LogManager.Configuration = config;
                        return LogManager.GetLogger(type);
                    }
                case "Test_ADXTargetStreamed":
                    {
                        var target = new ADXTarget
                        {
                            ConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new ArgumentNullException("CONNECTION_STRING not set"),
                            Database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set"),
                            TableName = m_generatedTableName,
                            UseStreamingIngestion = "true"
                        };
                        var config = new LoggingConfiguration();
                        config.AddTarget("adxtarget", target);
                        var rule = new LoggingRule("*", LogLevel.Debug, target);
                        config.LoggingRules.Add(rule);
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
        }

    }
}
