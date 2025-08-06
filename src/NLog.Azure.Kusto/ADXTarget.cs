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

        /// <summary>
        /// The name of the database to which data should be ingested to
        /// </summary>
        public string Database { get; set; }
        /// <summary>
        /// The name of the table to which data should be ingested to
        /// </summary>
        public string TableName { get; set; }
        /// <summary>
        /// Kusto connection string - Azure Data Explorer endpoint
        /// </summary>
        /// <remarks>
        /// Refer: <see href="https://learn.microsoft.com/azure/data-explorer/kusto/api/connection-strings/kusto" />
        /// </remarks>
        public Layout ConnectionString { get; set; }
        /// <summary>
        /// Override default application-name
        /// </summary>
        public Layout ApplicationName { get; set; }
        /// <summary>
        /// Override default application-version
        /// </summary>
        public Layout ApplicationVersion { get; set; }
        /// <summary>
        /// Whether to use streaming ingestion (reduced latency, at the cost of reduced throughput) or queued ingestion (increased latency, but much higher throughput).
        /// </summary>
        public string UseStreamingIngestion { get; set; } = "false";
        /// <summary>
        /// ManagedIdentity ClientId in case of user-assigned identity
        /// </summary>
        public string ManagedIdentityClientId
        {
            get => AuthenticationType == NLog.Azure.Kusto.AuthenticationType.AadSystemManagedIdentity ? "system" : _managedIdentityClientId;
            set
            {
                if (string.Equals(value, "system", StringComparison.OrdinalIgnoreCase))
                    AuthenticationType = NLog.Azure.Kusto.AuthenticationType.AadSystemManagedIdentity;
                else if (!string.IsNullOrEmpty(value))
                    AuthenticationType = NLog.Azure.Kusto.AuthenticationType.AadUserManagedIdentity;
                else
                    AuthenticationType = AuthenticationType != NLog.Azure.Kusto.AuthenticationType.AadUserManagedIdentity && AuthenticationType != NLog.Azure.Kusto.AuthenticationType.AadSystemManagedIdentity ? AuthenticationType : NLog.Azure.Kusto.AuthenticationType.None;
                _managedIdentityClientId = value;
            }
        }
        private string _managedIdentityClientId;
        public string AzCliAuth
        {
            get => AuthenticationType == NLog.Azure.Kusto.AuthenticationType.AddAzCli ? "true" : "false";
            set
            {
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    AuthenticationType = NLog.Azure.Kusto.AuthenticationType.AddAzCli;
                else
                    AuthenticationType = AuthenticationType != NLog.Azure.Kusto.AuthenticationType.AddAzCli ? AuthenticationType : NLog.Azure.Kusto.AuthenticationType.None;
            }
        }
        /// <summary>
        /// This property determines whether it is needed to flush the data immediately to ADX cluster,
        /// The default is false.
        /// </summary>
        public string FlushImmediately { get; set; } = "false";
        /// <summary>
        /// The name of the (pre-created) data mapping to use for the ingested data
        /// </summary>
        public string MappingNameRef { get; set; }
        /// <summary>
        /// Overrider default authentication-mode
        /// </summary>
        public Layout<AuthenticationType> AuthenticationType { get; set; } = NLog.Azure.Kusto.AuthenticationType.None;

        public ADXTarget()
        {
            Layout = "${logger}|${message}";
            IncludeEventProperties = true;
            RetryDelayMilliseconds = 50;    // Overwrite the default of 500ms
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();
            var defaultLogEvent = LogEventInfo.CreateNullEvent();
            // Validate required properties
            if (ConnectionString == null || string.IsNullOrWhiteSpace(RenderLogEvent(ConnectionString, defaultLogEvent)))
                throw new ArgumentNullException(nameof(ConnectionString), "ConnectionString is required.");
            if (string.IsNullOrWhiteSpace(Database))
                throw new ArgumentNullException(nameof(Database), "Database is required.");
            if (string.IsNullOrWhiteSpace(TableName))
                throw new ArgumentNullException(nameof(TableName), "TableName is required.");

            options = new ADXSinkOptions
            {
                ConnectionString = RenderLogEvent(ConnectionString, defaultLogEvent),
                DatabaseName = RenderLogEvent(Database, defaultLogEvent),
                TableName = RenderLogEvent(TableName, defaultLogEvent),
                ManagedIdentityClientId = RenderLogEvent(ManagedIdentityClientId, defaultLogEvent).NullIfEmpty(),
                AuthenticationType = RenderLogEvent(AuthenticationType, defaultLogEvent),
                UseStreamingIngestion = bool.Parse(RenderLogEvent(UseStreamingIngestion, defaultLogEvent).NullIfEmpty() ?? "false"),
                MappingName = RenderLogEvent(MappingNameRef, defaultLogEvent).NullIfEmpty(),
                FlushImmediately = bool.Parse(RenderLogEvent(FlushImmediately, defaultLogEvent).NullIfEmpty() ?? "false"),
                ApplicationName = RenderLogEvent(ApplicationName, defaultLogEvent).NullIfEmpty(),
                ApplicationVersion = RenderLogEvent(ApplicationVersion, defaultLogEvent).NullIfEmpty(),
            };

            m_streamingIngestion = options.UseStreamingIngestion;
            m_ingestionMapping = new IngestionMapping();

            if (!string.IsNullOrEmpty(options.MappingName))
            {
                m_ingestionMapping.IngestionMappingReference = options.MappingName;
            }

            var dmKcsb = options.GetIngestKcsb();
            var engineKcsb = options.GetEngineKcsb();

            m_ingestClient = options.UseStreamingIngestion
                ? KustoIngestFactory.CreateManagedStreamingIngestClient(engineKcsb, dmKcsb)
                : KustoIngestFactory.CreateQueuedIngestClient(dmKcsb);

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

            try
            {
                var sourceId = Guid.NewGuid();
                IKustoIngestionResult result = await IngestFromStreamAsync(sourceId, datastream).ConfigureAwait(false);
            }
            catch (global::Kusto.Data.Exceptions.KustoClientApplicationAuthenticationException ex)
            {
                NLog.Common.InternalLogger.Error(ex, "{0}: Authentication error. Please check your credentials.", this);
                return; // Swallow exception to avoid retry
            }
            catch (global::Kusto.Ingest.Exceptions.IngestClientException ex)
            {
                if (ex.IsPermanent)
                {
                    NLog.Common.InternalLogger.Error(ex, "{0}: Permanent ingestion failure to Kusto", this);
                    return; // Swallow exception to avoid retry
                }
                throw;
            }
        }

        protected override Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();    // Will never be hit, with override of WriteAsyncTask(IList<LogEventInfo> logEvents)
        }

        private Task<IKustoIngestionResult> IngestFromStreamAsync(Guid sourceId, Stream datastream)
        {
            if (m_streamingIngestion)
            {
                return m_ingestClient.IngestFromStreamAsync(datastream, new KustoIngestionProperties(options.DatabaseName, options.TableName)
                {
                    DatabaseName = options.DatabaseName,
                    TableName = options.TableName,
                    Format = DataSourceFormat.multijson,
                    IngestionMapping = m_ingestionMapping,
                }, new StreamSourceOptions
                {
                    SourceId = sourceId,
                    CompressionType = DataSourceCompressionType.GZip
                });
            }
            else
            {
                return m_ingestClient.IngestFromStreamAsync(datastream, new KustoQueuedIngestionProperties(options.DatabaseName, options.TableName)
                {
                    DatabaseName = options.DatabaseName,
                    TableName = options.TableName,
                    Format = DataSourceFormat.multijson,
                    IngestionMapping = m_ingestionMapping,
                    FlushImmediately = options.FlushImmediately,
                }, new StreamSourceOptions
                {
                    SourceId = sourceId,
                    CompressionType = DataSourceCompressionType.GZip
                });
            }
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
