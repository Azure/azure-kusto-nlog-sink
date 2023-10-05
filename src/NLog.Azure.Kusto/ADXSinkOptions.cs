using Kusto.Cloud.Platform.Utils;
using Kusto.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NLog.Azure.Kusto
{
    public class ADXSinkOptions
    {
        private const string AppName = "NLog.Azure.Kusto";
        private const string ClientVersion = "2.0.1";
        private const string IngestPrefix = "ingest-";
        private const string ProtocolSuffix = "://";

        /// <summary>
        /// Kusto connection string - Azure Data Explorer endpoint
        /// </summary>
        /// <remarks>
        /// Refer: https://learn.microsoft.com/en-us/azure/data-explorer/kusto/api/connection-strings/kusto
        /// </remarks>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The name of the database to which data should be ingested to
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// The name of the table to which data should be ingested to
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Whether to use streaming ingestion (reduced latency, at the cost of reduced throughput) or queued ingestion (increased latency, but much higher throughput).
        /// </summary>
        public bool UseStreamingIngestion { get; set; }

        /// <summary>
        /// The name of the (pre-created) data mapping to use for the ingested data
        /// </summary>
        public string MappingName { get; set; }

        /// <summary>
        /// This property determines whether it is needed to flush the data immediately to ADX cluster,
        /// The default is false.
        /// </summary>
        public bool FlushImmediately { get; set; }

        /// <summary>
        /// ManagedIdentity ClientId in case of user-assigned identity, set as 'system' for system-assigned identity
        /// </summary>
        public string ManagedIdentityClientId { get; set; }

        /// <summary>
        /// To use Azure Command line based authentication
        /// </summary>
        public bool AzCliAuth { get; set; }

        /// <summary>
        /// Override default application-name
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Override default application-version
        /// </summary>
        public string ApplicationVersion { get; set; }

        public KustoConnectionStringBuilder GetIngestKcsb()
        {
            // The connection string in most circumstances will not be an ingest endpoint. Just adding a double check on this.
            string dmConnectionStringEndpoint = ConnectionString.Contains(IngestPrefix) ? ConnectionString : ConnectionString.ReplaceFirstOccurrence(ProtocolSuffix, ProtocolSuffix + IngestPrefix);
            // For ingest we need not have all the options
            return GetKcsbWithAuthentication(dmConnectionStringEndpoint.Split("?")[0]);
        }

        public KustoConnectionStringBuilder GetEngineKcsb()
        {
            string engineConnectionStringEndpoint = ConnectionString.Contains(IngestPrefix) ? ConnectionString : ConnectionString.ReplaceFirstOccurrence(IngestPrefix, "");
            return GetKcsbWithAuthentication(engineConnectionStringEndpoint);
        }

        private KustoConnectionStringBuilder GetKcsbWithAuthentication(string connectionUrl)
        {
            KustoConnectionStringBuilder.DefaultPreventAccessToLocalSecretsViaKeywords = false;
            var baseKcsb = new KustoConnectionStringBuilder(connectionUrl);
            var kcsb = string.IsNullOrEmpty(ManagedIdentityClientId) ? baseKcsb : ("system".Equals(ManagedIdentityClientId, StringComparison.OrdinalIgnoreCase) ? baseKcsb.WithAadSystemManagedIdentity() : baseKcsb.WithAadUserManagedIdentity(ManagedIdentityClientId));
            kcsb = AzCliAuth ? kcsb.WithAadAzCliAuthentication() : kcsb;
            kcsb.ApplicationNameForTracing = AppName;
            kcsb.ClientVersionForTracing = ClientVersion;
            kcsb.SetConnectorDetails(AppName, ClientVersion, ApplicationName, ApplicationVersion ?? ClientVersion);
            return kcsb;
        }
    }
}
