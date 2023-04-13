using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using NLog.Azure.Kusto;
using NLog.Config;
using Xunit;

namespace NLog.Azure.Kusto.Tests
{
    public class ADXSInkE2ETest : IDisposable
    {
        private readonly string? m_generatedTableName;
        private readonly KustoConnectionStringBuilder? m_kustoConnectionStringBuilder;

        public ADXSInkE2ETest()
        {
            Assert.NotNull(Environment.GetEnvironmentVariable("CLUSTER_URI") ?? throw new ArgumentNullException("CLUSTER_URI not set"));
            Assert.NotNull(Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set"));
            Assert.NotNull(Environment.GetEnvironmentVariable("APP_ID") ?? throw new ArgumentNullException("APP_ID not set"));
            Assert.NotNull(Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? throw new ArgumentNullException("AZURE_TENANT_ID not set"));
            Assert.NotNull(Environment.GetEnvironmentVariable("APP_KEY") ?? throw new ArgumentNullException("APP_KEY not set"));

            var randomInt = new Random().Next();
            m_generatedTableName = "ADXNlogSink_" + randomInt;
            m_kustoConnectionStringBuilder = getConnectionStringBuilder("engine");

            using (var kustoClient = KustoClientFactory.CreateCslAdminProvider(m_kustoConnectionStringBuilder))
            {
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
                    Environment.GetEnvironmentVariable("DATABASE"), 
                    m_generatedTableName, 
                    new IngestionBatchingPolicy(TimeSpan.FromSeconds(5), 10, 1024));

                var enableStreamingIngestion = CslCommandGenerator.GenerateTableAlterStreamingIngestionPolicyCommand(
                    m_generatedTableName, 
                    true);
                
                kustoClient.ExecuteControlCommand(Environment.GetEnvironmentVariable("DATABASE"), command);
                kustoClient.ExecuteControlCommand(Environment.GetEnvironmentVariable("DATABASE"), alterBatchingPolicy);
                kustoClient.ExecuteControlCommand(Environment.GetEnvironmentVariable("DATABASE"), enableStreamingIngestion);
                //Buffer to get commands executed
                Thread.Sleep(50000);
            }
        }


        [Theory]
        [InlineData("Test_ADXTargetStreamed", 10, 30000)]
        [InlineData("Test_ADXNTargetBatched", 10 , 30000)]
        public async void Test_LogMessage(string testType, int numberOfLogs, int delayTime)
        {
            Logger logger = getCustomLogger(testType);
            for(int i=0; i<numberOfLogs; i++)
            {
                Console.WriteLine("_---------------------HEREEEEEEE");
                logger.Info("{type} Processed Info log {i}", testType, i);
                logger.Debug("{type} Processed debug Log {i}", testType, i);
                logger.Error(new Exception("{"+testType+"} : This is E2E Exception."));
            }
            await Task.Delay(delayTime);

            getConnectionStringBuilder(testType);
            using (var kustoClient = KustoClientFactory.CreateCslQueryProvider(m_kustoConnectionStringBuilder))
            {
                var reader = kustoClient.ExecuteQuery( m_generatedTableName + " | where Message contains \""+testType+"\" | count; " +
                    m_generatedTableName+ " | where Message contains \""+testType+"\" and  not(isempty(Exception))  | count");
                while (reader.Read())
                {
                    var count = reader.GetInt64(0);
                    Assert.Equal(3*numberOfLogs, count);
                }
                reader.NextResult();
                while (reader.Read())
                {
                    var exceptionCount = reader.GetInt64(0);
                    Assert.Equal( numberOfLogs, exceptionCount);
                }
            }
        }

        private Logger getCustomLogger(string type) 
        {
            switch (type)
            {
                case "Test_ADXNTargetBatched":
                    {
                        var target = new ADXTarget
                        {
                            IngestionEndpointUri = Environment.GetEnvironmentVariable("CLUSTER_URI") ?? throw new ArgumentNullException("CLUSTER_URI not set"),
                            Database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set"),
                            ApplicationClientId = Environment.GetEnvironmentVariable("APP_ID") ?? throw new ArgumentNullException("APP_ID not set"),
                            Authority = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? throw new ArgumentNullException("AZURE_TENANT_ID not set"),
                            ApplicationKey = Environment.GetEnvironmentVariable("APP_KEY") ?? throw new ArgumentNullException("APP_KEY not set"),
                            TableName = m_generatedTableName,
                            UseStreamingIngestion = "false",
                            AuthenticationMode = "AadApplicationKey"
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
                            IngestionEndpointUri = Environment.GetEnvironmentVariable("CLUSTER_URI") ?? throw new ArgumentNullException("CLUSTER_URI not set"),
                            Database = Environment.GetEnvironmentVariable("DATABASE") ?? throw new ArgumentNullException("DATABASE name not set"),
                            ApplicationClientId = Environment.GetEnvironmentVariable("APP_ID") ?? throw new ArgumentNullException("APP_ID not set"),
                            Authority = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? throw new ArgumentNullException("AZURE_TENANT_ID not set"),
                            ApplicationKey = Environment.GetEnvironmentVariable("APP_KEY") ?? throw new ArgumentNullException("APP_KEY not set"),
                            TableName = m_generatedTableName,
                            UseStreamingIngestion = "true",
                            AuthenticationMode = "AadApplicationKey"
                        };
                        var config = new LoggingConfiguration();
                        config.AddTarget("adxtarget", target);
                        var rule = new LoggingRule("*", LogLevel.Debug, target);
                        config.LoggingRules.Add(rule);
                        LogManager.Configuration = config;
                        return LogManager.GetLogger(type);
                    }

            }
            return null;
        }

        private KustoConnectionStringBuilder getConnectionStringBuilder(string type)
        {
            switch (type)
            {
                case "engine":
                case "Test_ADXNTargetBatched":
                    {
                        return new KustoConnectionStringBuilder(ADXSinkOptions.GetClusterUrl(
             Environment.GetEnvironmentVariable("CLUSTER_URI")),
         Environment.GetEnvironmentVariable("DATABASE"))
     .WithAadApplicationKeyAuthentication(Environment.GetEnvironmentVariable("APP_ID"),
         Environment.GetEnvironmentVariable("APP_KEY"), Environment.GetEnvironmentVariable("AZURE_TENANT_ID"));
                        
                    }
                case "dm":
                case "Test_ADXTargetStreamed":
                    {
                        return new KustoConnectionStringBuilder(
             Environment.GetEnvironmentVariable("CLUSTER_URI"),
         Environment.GetEnvironmentVariable("DATABASE"))
     .WithAadApplicationKeyAuthentication(Environment.GetEnvironmentVariable("APP_ID"),
         Environment.GetEnvironmentVariable("APP_KEY"), Environment.GetEnvironmentVariable("AZURE_TENANT_ID"));

                    }
            }
            return null;
        }

        public void Dispose()
        {
            /*using (var queryProvider = KustoClientFactory.CreateCslAdminProvider(m_kustoConnectionStringBuilder))
            {
                var command = CslCommandGenerator.GenerateTableDropCommand(m_generatedTableName);
                var clientRequestProperties = new ClientRequestProperties()
                {
                    ClientRequestId = Guid.NewGuid().ToString()
                };
                queryProvider.ExecuteControlCommand(Environment.GetEnvironmentVariable("DATABASE"), command,
                    clientRequestProperties);
            }*/
        }

    }
}
