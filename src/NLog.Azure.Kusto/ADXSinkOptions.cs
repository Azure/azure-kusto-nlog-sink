using Kusto.Data;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using Kusto.Cloud.Platform.Utils;

namespace NLog.Azure.Kusto
{
    public class ADXSinkOptions
    {
        private const string AppName = "NLog.Azure.Kusto";
        private const string ClientVersion = "2.0.0";
        private const string IngestPrefix = "ingest-";
        private const string ProtocolSuffix = "://";
        /// <summary>
        /// Azure Data Explorer endpoint (Ingestion endpoint for Queued Ingestion, Query endpoint for Streaming Ingestion)
        /// </summary>
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

        public KustoConnectionStringBuilder GetKustoConnectionStringBuilder(bool isDm)
        {
            KustoConnectionStringBuilder.DefaultPreventAccessToLocalSecretsViaKeywords = false;
            string dmConnectionStringEndpoint = this.ConnectionString.Contains(IngestPrefix) ? this.ConnectionString : this.ConnectionString.ReplaceFirstOccurrence(ProtocolSuffix, ProtocolSuffix + IngestPrefix);
            string engineConnectionStringEndpoint = !this.ConnectionString.Contains(IngestPrefix) ? this.ConnectionString : this.ConnectionString.ReplaceFirstOccurrence(IngestPrefix, "");
            string connectionString = isDm ? dmConnectionStringEndpoint : engineConnectionStringEndpoint;
           
            KustoConnectionStringBuilder kcsb = new KustoConnectionStringBuilder(connectionString);
            // Check if this is a case of managed identity
            if (!string.IsNullOrEmpty(this.ManagedIdentityClientId))
            {
                if ("system".Equals(this.ManagedIdentityClientId))
                {
                    kcsb = kcsb.WithAadSystemManagedIdentity();
                }
                else
                {
                    kcsb = kcsb.WithAadUserManagedIdentity(this.ManagedIdentityClientId);
                }
            }

            if (this.AzCliAuth)
            {
                kcsb = kcsb.WithAadAzCliAuthentication();
            }
            kcsb.ApplicationNameForTracing = AppName;
            kcsb.ClientVersionForTracing = ClientVersion;
            kcsb.SetConnectorDetails(AppName, ClientVersion, "Nlog", "5.1.4");
            return kcsb;
        }
    }
}
