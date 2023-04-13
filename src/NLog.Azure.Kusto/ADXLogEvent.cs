using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog.Layouts;
using NLog.Targets;

namespace NLog.Azure.Kusto
{
    class ADXLogEvent
    {
        public string Level { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public string FormattedMessage { get; set; }
        public string Properties { get; set; }
        public static ADXLogEvent GetADXLogEvent(LogEventInfo logEventInfo, string renderedMessage)
        {
            return new ADXLogEvent
            {
                Level = logEventInfo.Level.ToString(),
                Timestamp = logEventInfo.TimeStamp,
                Message = logEventInfo.FormattedMessage,
                FormattedMessage = renderedMessage,
                Exception = logEventInfo.Exception?.ToString(),
                Properties = logEventInfo.Properties?.Count == 0 ? "{}" : System.Text.Json.JsonSerializer.Serialize(logEventInfo.Properties)
            };
        }
    }
}
