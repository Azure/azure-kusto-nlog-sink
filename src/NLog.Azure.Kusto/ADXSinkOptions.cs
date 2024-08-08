using System;
using System.Collections.Generic;
using System.Linq;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data;

namespace NLog.Azure.Kusto
{
    internal sealed class ADXSinkOptions
    {
        private const string AppName = "NLog.Azure.Kusto";
        private const string ClientVersion = "2.1.0";
        private const string IngestPrefix = "ingest-";
        private const string ProtocolSuffix = "://";

        /// <inheritdoc cref="ADXTarget.ConnectionString"/>
        public string ConnectionString { get; set; }

        /// <inheritdoc cref="ADXTarget.DatabaseName"/>
        public string DatabaseName { get; set; }

        /// <inheritdoc cref="ADXTarget.TableName"/>
        public string TableName { get; set; }

        /// <inheritdoc cref="ADXTarget.UseStreamingIngestion"/>
        public bool UseStreamingIngestion { get; set; }

        /// <inheritdoc cref="ADXTarget.MappingNameRef"/>
        public string MappingName { get; set; }

        /// <inheritdoc cref="ADXTarget.FlushImmediately"/>
        public bool FlushImmediately { get; set; }

        /// <inheritdoc cref="ADXTarget.ManagedIdentityClientId"/>
        public string ManagedIdentityClientId { get; set; }

        /// <inheritdoc cref="ADXTarget.AuthenticationType"/>
        public AuthenticationType AuthenticationType { get; set; }

        /// <inheritdoc cref="ADXTarget.ApplicationName"/>
        public string ApplicationName { get; set; }

        /// <inheritdoc cref="ADXTarget.ApplicationVersion"/>
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
            var kcsb = new KustoConnectionStringBuilder(connectionUrl);
            switch (AuthenticationType)
            {
                case AuthenticationType.AadUserManagedIdentity:
                    kcsb = kcsb.WithAadUserManagedIdentity(ManagedIdentityClientId);
                    break;
                case AuthenticationType.AadSystemManagedIdentity:
                    kcsb = kcsb.WithAadSystemManagedIdentity();
                    break;
                case AuthenticationType.AadWorkloadIdentity:
                    kcsb = kcsb.WithAadAzureTokenCredentialsAuthentication(new global::Azure.Identity.WorkloadIdentityCredential());
                    break;
                case AuthenticationType.AadUserPrompt:
                    kcsb = kcsb.WithAadAzureTokenCredentialsAuthentication(new global::Azure.Identity.ChainedTokenCredential(new global::Azure.Identity.AzureCliCredential(), new global::Azure.Identity.InteractiveBrowserCredential()));
                    break;
                case AuthenticationType.AddAzCli:
                    kcsb = kcsb.WithAadAzCliAuthentication();
                    break;
            }

            kcsb.ApplicationNameForTracing = AppName;
            kcsb.ClientVersionForTracing = ClientVersion;
            kcsb.SetConnectorDetails(AppName, ClientVersion, ApplicationName, ApplicationVersion ?? ClientVersion);
            return kcsb;
        }
    }
}
