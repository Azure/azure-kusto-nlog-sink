namespace NLog.Azure.Kusto
{
    public class SinkColumnMapping
    {
        public string ColumnName { get; set; } = string.Empty;
        public string ColumnType { get; set; } = string.Empty;
        public string ValuePath { get; set; } = string.Empty;

        public SinkColumnMapping() { }
    }
}
