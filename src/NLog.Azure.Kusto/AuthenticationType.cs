using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLog.Azure.Kusto
{
    /// <summary>
    /// Possible options for override of authentication
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// Use the authentication details provided by the ConnectionString
        /// </summary>
        None = 0,
        /// <summary>
        /// User assigned managed identity
        /// </summary>
        AadUserManagedIdentity = 1,
        /// <summary>
        /// System assigned managed identity
        /// </summary>
        AadSystemManagedIdentity = 2,
        /// <summary>
        /// Workload identity for kubernetes and other hosts
        /// </summary>
        AadWorkloadIdentity = 3,
        /// <summary>
        /// AzureCliCredential with fallback to InteractiveBrowserCredential
        /// </summary>
        AadUserPrompt = 4,
        /// <summary>
        /// Azure CLI Authentication
        /// </summary>
        AddAzCli = 5,
    }
}
