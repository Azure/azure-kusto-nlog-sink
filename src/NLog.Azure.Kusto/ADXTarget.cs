using System;
using Kusto.Ingest;
using Kusto.Data;
using NLog.Targets;
using System.IO;
using Microsoft.IO;
using System.IO.Compression;
using System.Text.Json;
using Kusto.Data.Common;
using NLog.Config;

namespace NLog.Azure.Kusto
{
    [Target("ADXTarget")]
    public class ADXTarget : TargetWithLayout
    {
        ADXSinkOptions options;
        private IKustoIngestClient m_ingestClient;
        private IngestionMapping m_ingestionMapping;
        private bool m_disposed;
        private bool m_streamingIngestion;
        private int m_ingestionTimeout; //seconds
        private static readonly RecyclableMemoryStreamManager SRecyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
        
        [RequiredParameter]
        public string Database { get; set; }
        [RequiredParameter]
        public string TableName { get; set; }
        [RequiredParameter]
        public string IngestionEndpointUri { get; set; }
        public string UseStreamingIngestion { get; set; } = "false";
        public string AuthenticationMode { get; set; }
        public string ApplicationClientId { get; set; }
        public string ApplicationKey { get; set; }
        public string Authority { get; set; }
        public string ManagedIdentityClientId { get; set; }
        public string FlushImmediately { get; set; } = "false";
        public string MappingNameRef { get; set; }

     
        public string IngestionTimout { get; set; }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            var defaultLogEvent = LogEventInfo.CreateNullEvent();
            options = new ADXSinkOptions
            {
                DatabaseName = RenderLogEvent(Database, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(nameof(Database)),
                IngestionEndpointUri = RenderLogEvent(IngestionEndpointUri, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(IngestionEndpointUri),
                TableName = RenderLogEvent(TableName, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(TableName),
                UseStreamingIngestion = bool.Parse(RenderLogEvent(UseStreamingIngestion, defaultLogEvent)),
                MappingName = RenderLogEvent(MappingNameRef, defaultLogEvent),
                FlushImmediately = bool.Parse(RenderLogEvent(FlushImmediately, defaultLogEvent)),
            };

            setupAuthCredentials(options, defaultLogEvent);
            m_streamingIngestion = options.UseStreamingIngestion;
            m_ingestionMapping = new IngestionMapping();
            m_ingestionTimeout = RenderLogEvent(IngestionTimout, defaultLogEvent) == "" ? 0 : int.Parse(RenderLogEvent(IngestionTimout, defaultLogEvent));

            if (!string.IsNullOrEmpty(options.MappingName))
            {
                m_ingestionMapping.IngestionMappingReference = options.MappingName;
            }

            KustoConnectionStringBuilder dmkcsb = options.GetKustoConnectionStringBuilder(Constants.CONNECTION_STRING_TYPE.DATA_MANAGEMENT);
            KustoConnectionStringBuilder engineKcsb = options.GetKustoConnectionStringBuilder(Constants.CONNECTION_STRING_TYPE.DATA_ENGINE);

            m_ingestClient = options.UseStreamingIngestion
                ? KustoIngestFactory.CreateManagedStreamingIngestClient(engineKcsb, dmkcsb)
                : KustoIngestFactory.CreateQueuedIngestClient(dmkcsb);

        }

        private void setupAuthCredentials(ADXSinkOptions options, LogEventInfo defaultLogEvent)
        {
            string appId = RenderLogEvent(ApplicationClientId, defaultLogEvent).NullIfEmpty();

            if (appId != null)
            {
                options.ApplicationClientId = appId;
                options.ApplicationKey = RenderLogEvent(ApplicationKey, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(nameof(ApplicationKey));
                options.Authority = RenderLogEvent(Authority, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(nameof(Authority));
                options.AuthenticationMode = Kusto.AuthenticationMode.AadApplicationKey;
            }
            else
            {
                options.ManagedIdentityClientId = RenderLogEvent(ManagedIdentityClientId, defaultLogEvent).NullIfEmpty();
                options.AuthenticationMode = Kusto.AuthenticationMode.ManagedIdentity;
            }
        }

        protected override async void Write(LogEventInfo logEvent)
        {

            using (var datastream = CreateStreamFromLogEvents(ADXLogEvent.GetADXLogEvent(logEvent, RenderLogEvent(Layout, logEvent))))
            {
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
                        IngestionMapping = m_ingestionMapping
                    }, new StreamSourceOptions
                    {
                        SourceId = sourceId,
                        CompressionType = DataSourceCompressionType.GZip
                    }).ConfigureAwait(false);
                }
            }
        }

        private Stream CreateStreamFromLogEvents(ADXLogEvent adxloginfo)
        {
            var stream = SRecyclableMemoryStreamManager.GetStream();

            using (GZipStream compressionStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
            {
                JsonSerializer.Serialize(compressionStream, adxloginfo);
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
    public static class StringExtensions
    {
        public static string NullIfEmpty(this string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }

}
