using NLog.Config;
using Xunit;

namespace NLog.Azure.Kusto.Tests
{
    public class ADXTargetTest
    {
        private static ADXTarget GetTarget(string configfile)
        {
            LogManager.Setup().LoadConfigurationFromFile(Path.Combine("config", configfile + ".config"), optional: false);
            return LogManager.Configuration.AllTargets.OfType<ADXTarget>().First();
        }

        [Fact]
        public void Test_ConfigLoadsCorrectly()
        {
            var target = GetTarget("adxtarget");

            Assert.Equal("Data Source=https://somecluster.eastus.dev.kusto.windows.net/;Database=NetDefaultDB;Fed=True", target.ConnectionString.ToString());
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
