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
            Assert.Equal("Data Source=https://somecluster.westeurope.dev.kusto.windows.net;Database=e2e;Fed=True;AppClientId=APP-ID;AppKey=APP-KEY;Authority Id=TENANT-ID", target.ConnectionString);
            Assert.Equal("ADXNlog", target.TableName);
            Assert.Equal("e2e", target.Database);
            Assert.Equal("false", target.UseStreamingIngestion);
        }

        [Fact]
        public void Test_ErrorConfigLoads()
        {
            Assert.Throws<NLog.NLogConfigurationException>(() => GetTarget("adxtargeterror"));
        }
    }
}
