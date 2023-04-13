namespace NLog.Azure.Kusto.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger logger = LogManager.GetCurrentClassLogger();

            try
            {
                Console.WriteLine("Sending logs to ADX");
                logger.Info("Here is the info message.");
                var position = new
                {
                    Latitude = 25,
                    Longitude = 134
                };
                var elapsedMs = 34;
                for (int i = 0; i < 5; i++)
                {
                    logger.Info("Processed {@Position} in {Elapsed:000} ms.", position, elapsedMs);
                }
                throw new Exception("Custom EXCEPTION raised");

            }
            catch (Exception e)
            {
                logger.Error(e, "This was exception");
                Thread.Sleep(10000);
            }
        }
    }
}
