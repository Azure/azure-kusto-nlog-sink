using NLog.Config;
using Xunit;

namespace NLog.Azure.Kusto.Tests
{
    public class ADXTargetTest
    {
        private static ADXTarget GetTarget(string configfile)
        {
            LogManager.Configuration = new XmlLoggingConfiguration(Path.Combine("config", configfile + ".config"), true);
            return LogManager.Configuration.AllTargets.OfType<ADXTarget>().First();
        }

        [Fact]
        public void Test_ConfigLoadsCorrectly()
        {
            var target = GetTarget("adxtarget");

            Assert.Equal("appkey", target.ApplicationKey);
            Assert.Equal("someclientid", target.ApplicationClientId);
            Assert.Equal("someauthid", target.Authority);
            Assert.Equal("https://somecluster.eastus.dev.kusto.windows.net/", target.IngestionEndpointUri);
            Assert.Equal("ADXNlog", target.TableName);
            Assert.Equal("testdb", target.Database);
            Assert.Equal("false", target.UseStreamingIngestion);
        }

        [Fact]
        public void Test_ErrorConfigLoads()
        {
            Assert.Throws<NLog.NLogConfigurationException>(() => GetTarget("adxtargeterror"));
        }
    }
}
