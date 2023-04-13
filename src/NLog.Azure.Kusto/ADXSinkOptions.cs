using Kusto.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NLog.Azure.Kusto
{
    public class ADXSinkOptions
    {
        /// <summary>
        /// Azure Data Explorer endpoint (Ingestion endpoint for Queued Ingestion, Query endpoint for Streaming Ingestion)
        /// </summary>
        public string IngestionEndpointUri { get; set; }

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
        /// The explicit columns mapping to use for the ingested data
        /// </summary>
        public IEnumerable<SinkColumnMapping> ColumnsMapping { get; set; }

        /// <summary>
        /// This property determines whether it is needed to flush the data immediately to ADX cluster,
        /// The default is false.
        /// </summary>
        public bool FlushImmediately { get; set; }

        /// <summary>
        /// determines the authentication mode
        /// </summary>
        public AuthenticationMode AuthenticationMode { get; set; }

        /// <summary>
        /// application clientId
        /// </summary>
        public string ApplicationClientId { get; set; }

        /// <summary>
        /// ApplicationKey
        /// </summary>
        public string ApplicationKey { get; set; }

        /// <summary>
        /// Authority
        /// </summary>
        public string Authority { get; set; }

        /// <summary>
        /// ManagedIdentity ClientId in case of user-assigned identity
        /// </summary>
        public string ManagedIdentityClientId { get; set; }

        public static readonly Dictionary<string, AuthenticationMode> AuthenticationModeMap = new Dictionary<string, AuthenticationMode>
        {
                { Constants.AUTHENTICATION_TYPES.AadApplicationKey, AuthenticationMode.AadApplicationKey },
                { Constants.AUTHENTICATION_TYPES.ManagedIdentity, AuthenticationMode.ManagedIdentity }
        };

        public KustoConnectionStringBuilder GetKustoConnectionStringBuilder(string type)
        {
            KustoConnectionStringBuilder kcsb = null;
            switch (type)
            {
                case Constants.CONNECTION_STRING_TYPE.DATA_MANAGEMENT:
                    {
                        kcsb = new KustoConnectionStringBuilder(this.IngestionEndpointUri, this.DatabaseName);
                        break;
                    }
                case Constants.CONNECTION_STRING_TYPE.DATA_ENGINE:
                    {
                        kcsb = new KustoConnectionStringBuilder(GetClusterUrl(this.IngestionEndpointUri), this.DatabaseName);
                        break;
                    }
            }
            return GetKcsbWithAuthentication(kcsb);
        }

        protected KustoConnectionStringBuilder GetKcsbWithAuthentication(KustoConnectionStringBuilder kcsb)
        {
            if (kcsb == null)
            {
                throw new ArgumentException("KustoConnectionStringBuilder cannot be null");
            }

            switch (this.AuthenticationMode)
            {
                case AuthenticationMode.AadApplicationKey:
                    {
                        kcsb = kcsb.WithAadApplicationKeyAuthentication(this.ApplicationClientId, this.ApplicationKey, this.Authority);
                        break;
                    }
                case AuthenticationMode.ManagedIdentity:
                    {
                        if (string.IsNullOrEmpty(this.ManagedIdentityClientId))
                            kcsb = kcsb.WithAadSystemManagedIdentity();
                        else kcsb = kcsb.WithAadUserManagedIdentity(this.ManagedIdentityClientId);
                        break;
                    }
            }
            return kcsb;
        }

        public static string GetClusterUrl(string ingestUrl)
        {
            string[] parts = ingestUrl.Split('-');
            string clusterName = parts.Last();
            return "https://" + clusterName;
        }
    }

    public enum AuthenticationMode
    {
        AadApplicationKey,
        ManagedIdentity,
    }
}
