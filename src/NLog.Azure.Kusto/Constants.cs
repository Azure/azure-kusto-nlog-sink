namespace NLog.Azure.Kusto
{
    public static class Constants
    {
        public static string EMPTY_STRING = "";

        public static class CONNECTION_STRING_TYPE
        {
            public const string DATA_MANAGEMENT = "data_management";
            public const string DATA_ENGINE = "data_engine";
        }

        public static class AUTHENTICATION_TYPES
        {
            public const string AadApplicationKey = "AadApplicationKey";
            public const string ManagedIdentity = "AadManagedIdentity";
        }
    }
}
