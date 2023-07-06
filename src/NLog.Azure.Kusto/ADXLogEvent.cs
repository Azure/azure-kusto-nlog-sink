using System.Text.Json;
using System.Text.Json.Serialization;

namespace NLog.Azure.Kusto
{
    class ADXLogEvent
    {
        public string Level { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public string FormattedMessage { get; set; }
        [JsonConverter(typeof(UnsafeRawJsonConverter))]
        public string Properties { get; set; }
        public static ADXLogEvent GetADXLogEvent(LogEventInfo logEventInfo, string renderedMessage, string renderedJsonProperties)
        {
            return new ADXLogEvent
            {
                Level = logEventInfo.Level.ToString(),
                Timestamp = logEventInfo.TimeStamp.ToUniversalTime(),
                Message = logEventInfo.Message,
                FormattedMessage = renderedMessage,
                Exception = logEventInfo.Exception?.ToString(),
                Properties = renderedJsonProperties,
            };
        }
    }

    /// <summary>
    /// Serializes the contents of a string value as raw JSON.  The string is validated as being an RFC 8259-compliant JSON payload
    /// </summary>
    public class RawJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.GetRawText();
        }

        protected virtual bool SkipInputValidation => false;

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            // skipInputValidation : true will improve performance, but only do this if you are certain the value represents well-formed JSON!
            writer.WriteRawValue(value, skipInputValidation: SkipInputValidation);
    }

    /// <summary>
    /// Serializes the contents of a string value as raw JSON.  The string is NOT validated as being an RFC 8259-compliant JSON payload
    /// </summary>
    public class UnsafeRawJsonConverter : RawJsonConverter
    {
        protected override bool SkipInputValidation => true;
    }
}
