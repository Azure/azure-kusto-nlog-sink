using System;
using Kusto.Ingest;
using Kusto.Data;
using NLog.Targets;
using System.IO;
using System.Collections.Generic;
using Microsoft.IO;
using System.IO.Compression;
using System.Text.Json;
using Kusto.Data.Common;
using System.Linq;
using NLog.Config;
using System.Diagnostics;

namespace NLog.Azure.Kusto
{
    [Target("ADXTarget")]
    public class ADXTarget : TargetWithLayout
    {
        ADXSinkOptions options;
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        private IKustoIngestClient m_ingestClient;
        private IngestionMapping m_ingestionMapping;
        private bool m_disposed;
        private bool m_streamingIngestion;
        private int m_ingestionTimeout = 0; //seconds
        private static readonly RecyclableMemoryStreamManager SRecyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
        private static readonly List<ColumnMapping> DefaultIngestionColumnMapping = new List<ColumnMapping>
        {
            new ColumnMapping
            {
                ColumnName = "Timestamp",
                ColumnType = "datetime",
                Properties = new Dictionary<string, string>
                {
                    {
                        MappingConsts.Path, "$.Timestamp"
                    }
                }
            },
            new ColumnMapping
            {
                ColumnName = "Level",
                ColumnType = "string",
                Properties = new Dictionary<string, string>
                {
                    {
                        MappingConsts.Path, "$.Level"
                    }
                }
            },
            new ColumnMapping
            {
                ColumnName = "Message",
                ColumnType = "string",
                Properties = new Dictionary<string, string>
                {
                    {
                        MappingConsts.Path, "$.Message"
                    }
                }
            },
            new ColumnMapping
            {
                ColumnName = "FormattedMessage",
                ColumnType = "string",
                Properties = new Dictionary<string, string>
                {
                    {
                        MappingConsts.Path, "$.FormattedMessage"
                    }
                }
            },
            new ColumnMapping
            {
                ColumnName = "Exception",
                ColumnType = "string",
                Properties = new Dictionary<string, string>
                {
                    {
                        MappingConsts.Path, "$.Exception"
                    }
                }
            },
                        new ColumnMapping
            {
                ColumnName = "Properties",
                ColumnType = "dynamic",
                Properties = new Dictionary<string, string>
                {
                    {
                        MappingConsts.Path, "$.Properties"
                    }
                }
            }
        };

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
        public string ColumnsMapping { get; set; }
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
                AuthenticationMode = ADXSinkOptions.AuthenticationModeMap.GetValueOrDefault(RenderLogEvent(AuthenticationMode, defaultLogEvent)),
                MappingName = RenderLogEvent(MappingNameRef, defaultLogEvent),
                ColumnsMapping = !string.IsNullOrEmpty(ColumnsMapping) ? JsonSerializer.Deserialize<SinkColumnMapping[]>(RenderLogEvent(ColumnsMapping, defaultLogEvent)) : null,
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
            else if (options.ColumnsMapping?.Any() == true)
            {
                m_ingestionMapping.IngestionMappings = options.ColumnsMapping.Select(m => new ColumnMapping
                {
                    ColumnName = m.ColumnName,
                    ColumnType = m.ColumnType,
                    Properties = new Dictionary<string, string>(1)
                    {
                        {
                            MappingConsts.Path, m.ValuePath
                        }
                    }
                }).ToList();
            }
            else
            {
                m_ingestionMapping.IngestionMappings = DefaultIngestionColumnMapping;
            }


            KustoConnectionStringBuilder dmkcsb = options.GetKustoConnectionStringBuilder(Constants.CONNECTION_STRING_TYPE.DATA_MANAGEMENT);
            KustoConnectionStringBuilder engineKcsb = options.GetKustoConnectionStringBuilder(Constants.CONNECTION_STRING_TYPE.DATA_ENGINE);

            m_ingestClient = options.UseStreamingIngestion
                ? KustoIngestFactory.CreateManagedStreamingIngestClient(engineKcsb, dmkcsb)
                : KustoIngestFactory.CreateQueuedIngestClient(dmkcsb);

        }

        private void setupAuthCredentials(ADXSinkOptions options, LogEventInfo defaultLogEvent)
        {
            switch (options.AuthenticationMode)
            {
                case Kusto.AuthenticationMode.AadApplicationKey:
                    {
                        options.ApplicationKey = RenderLogEvent(ApplicationKey, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(nameof(ApplicationKey));
                        options.Authority = RenderLogEvent(Authority, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(nameof(Authority));
                        options.ApplicationClientId = RenderLogEvent(ApplicationClientId, defaultLogEvent).NullIfEmpty() ?? throw new ArgumentNullException(nameof(ApplicationClientId));
                        break;
                    }
                case Kusto.AuthenticationMode.ManagedIdentity:
                    {
                        options.ManagedIdentityClientId = RenderLogEvent(ManagedIdentityClientId, defaultLogEvent).NullIfEmpty();
                        break;
                    }
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
                        LeaveOpen = false,
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
                        LeaveOpen = false,
                        CompressionType = DataSourceCompressionType.GZip
                    }).ConfigureAwait(false);
                }
                var ingestionStatus = result.GetIngestionStatusBySourceId(sourceId);
                var stopWatch = Stopwatch.StartNew();
                while (true)
                {
                    // check if the record is updated
                    if (ingestionStatus.Status != Status.Pending ||
                        m_ingestionTimeout != 0 && stopWatch.Elapsed.TotalSeconds > m_ingestionTimeout)
                    {
                        break; // the record is updated or timed out, exit the loop!
                    }
                }
                if (ingestionStatus.Status == Status.Failed)
                {
                    logger.Error("Kusto log ingestion failed for record: {0} with ActivityId {1}, Error Code: {2}", sourceId, ingestionStatus.ActivityId, ingestionStatus.ErrorCode);
                }
                else if (ingestionStatus.Status == Status.Succeeded)
                {
                    logger.Debug("ADX sink: Ingestion succeeded for record {0}, with ActivityId {1} ", sourceId, ingestionStatus.ActivityId);
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
