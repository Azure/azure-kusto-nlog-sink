using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Kusto.Ingest;
using Kusto.Data;
using Kusto.Data.Common;
using Microsoft.IO;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace NLog.Azure.Kusto
{
    [Target("ADXTarget")]
    public class ADXTarget : AsyncTaskTarget
    {
        ADXSinkOptions options;
        private IKustoIngestClient m_ingestClient;
        private IngestionMapping m_ingestionMapping;
        private bool m_disposed;
        private bool m_streamingIngestion;
        private static readonly RecyclableMemoryStreamManager SRecyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
        private readonly JsonLayout _jsonLayoutProperties = new JsonLayout() { IncludeEventProperties = true, MaxRecursionLimit = 10 };

        [RequiredParameter]
        public string Database { get; set; }
        [RequiredParameter]
        public string TableName { get; set; }
        [RequiredParameter]
        public string ConnectionString { get; set; }
        public string UseStreamingIngestion { get; set; } = "false";
        public string ManagedIdentityClientId { get; set; }
        public string FlushImmediately { get; set; } = "false";
        public string MappingNameRef { get; set; }

        public ADXTarget()
        {
            Layout = "${logger}|${message}";
            IncludeEventProperties = true;
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            var defaultLogEvent = LogEventInfo.CreateNullEvent();
            options = new ADXSinkOptions
            {
                DatabaseName = RenderLogEvent(Database, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(nameof(Database)),
                ConnectionString = RenderLogEvent(ConnectionString, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(ConnectionString),
                TableName = RenderLogEvent(TableName, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(TableName),
                UseStreamingIngestion = bool.Parse(RenderLogEvent(UseStreamingIngestion, defaultLogEvent)),
                MappingName = RenderLogEvent(MappingNameRef, defaultLogEvent),
                FlushImmediately = bool.Parse(RenderLogEvent(FlushImmediately, defaultLogEvent)),
                ManagedIdentityClientId = RenderLogEvent(ManagedIdentityClientId, defaultLogEvent).NullIfEmpty(),
            };
            m_streamingIngestion = options.UseStreamingIngestion;
            m_ingestionMapping = new IngestionMapping();

            if (!string.IsNullOrEmpty(options.MappingName))
            {
                m_ingestionMapping.IngestionMappingReference = options.MappingName;
            }

            KustoConnectionStringBuilder dmkcsb = options.GetKustoConnectionStringBuilder(true);
            KustoConnectionStringBuilder engineKcsb = options.GetKustoConnectionStringBuilder(false);

            m_ingestClient = options.UseStreamingIngestion
                ? KustoIngestFactory.CreateManagedStreamingIngestClient(engineKcsb, dmkcsb)
                : KustoIngestFactory.CreateQueuedIngestClient(dmkcsb);

            _jsonLayoutProperties.IncludeEventProperties = IncludeEventProperties;
            if (ContextProperties?.Count > 0)
            {
                _jsonLayoutProperties.Attributes.Clear();
                foreach (var contextProperty in ContextProperties)
                {
                    _jsonLayoutProperties.Attributes.Add(new JsonAttribute(contextProperty.Name, contextProperty.Layout));
                }
            }
        }

        protected override void CloseTarget()
        {
            try
            {
                m_ingestClient?.Dispose();
            }
            finally
            {
                m_ingestClient = null;
            }
        }

        protected override async Task WriteAsyncTask(IList<LogEventInfo> logEvents, CancellationToken cancellationToken)
        {
            using var datastream = CreateStreamFromLogEvents(logEvents);
            
            var sourceId = Guid.NewGuid();
            IKustoIngestionResult result;
            if (m_streamingIngestion)
            {
                result = await m_ingestClient.IngestFromStreamAsync(datastream, new KustoIngestionProperties(options.DatabaseName, options.TableName)
                {
                    DatabaseName = options.DatabaseName,
                    TableName = options.TableName,
                    Format = DataSourceFormat.multijson,
                    IngestionMapping = m_ingestionMapping
                }, new StreamSourceOptions
                {
                    SourceId = sourceId,
                    CompressionType = DataSourceCompressionType.GZip
                }).ConfigureAwait(false);
            }
            else
            {
                result = await m_ingestClient.IngestFromStreamAsync(datastream, new KustoQueuedIngestionProperties(options.DatabaseName, options.TableName)
                {
                    DatabaseName = options.DatabaseName,
                    TableName = options.TableName,
                    Format = DataSourceFormat.multijson,
                    IngestionMapping = m_ingestionMapping,
                    FlushImmediately = options.FlushImmediately,
                    ReportLevel = IngestionReportLevel.FailuresAndSuccesses,
                    ReportMethod = IngestionReportMethod.Table
                }, new StreamSourceOptions
                {
                    SourceId = sourceId,
                    CompressionType = DataSourceCompressionType.GZip
                }).ConfigureAwait(false);
            }
        }

        protected override Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();    // Will never be hit, with override of WriteAsyncTask(IList<LogEventInfo> logEvents)
        }

        private Stream CreateStreamFromLogEvents(IList<LogEventInfo> batch)
        {
            var stream = SRecyclableMemoryStreamManager.GetStream();
            using (GZipStream compressionStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
            {
                for (int i = 0; i < batch.Count; ++i)
                {
                    var logEvent = batch[i];
                    var logEventMessage = RenderLogEvent(Layout, logEvent);
                    var logEventJsonProperties = RenderLogEvent(_jsonLayoutProperties, logEvent).NullIfEmpty() ?? "{}";
                    var adxloginfo = ADXLogEvent.GetADXLogEvent(logEvent, logEventMessage, logEventJsonProperties);
                    JsonSerializer.Serialize(compressionStream, adxloginfo);
                }
            }

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        #region IDisposable methods
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (m_disposed)
            {
                return;
            }

            if (disposing)
            {
                m_ingestClient?.Dispose();
                m_ingestClient = null;
            }
            m_disposed = true;
        }
        #endregion
    }

    internal static class StringExtensions
    {
        public static string NullIfEmpty(this string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
