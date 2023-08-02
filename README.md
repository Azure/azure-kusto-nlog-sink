# NLog.Azure.Kusto

An Azure Data Explorer(ADX) custom target that writes log events to an [Azure Data Explorer (Kusto)](https://docs.microsoft.com/en-us/azure/data-explorer) cluster.

**Package** - [NLog.Azure.Kusto](http://nuget.org/packages/nlog.azure.kusto)
| **Platforms** - .Net 6.0

## Getting started

Install from [NuGet]():

```powershell
Install-Package NLog.Azure.Kusto
```

## Configuration

Add the ADX target to your NLog configuration:

```xml
<nlog>
  <extensions>
    <add assembly="NLog.Azure.Kusto"/>
  </extensions>
  <targets>
   <!--  ADX target -->
    <target name="adxtarget" type="ADXTarget"
      IngestionEndpointUri="<ADX connection string>"
      Database="<ADX database name>"
      TableName="<ADX table name>"
      ApplicationClientId="<AAD App clientId>"
      ApplicationKey="<AAD App key>"
      Authority="<AAD tenant id>">
        <contextproperty name="HostName" layout="${hostname}" />  <!-- Repeatable, optional -->
    </target>
  </targets>
  <rules>
    <logger minlevel="Info" name="*" writeTo="adxtarget"/>
  </rules>
</nlog>
```

## Available Configuration Options

| Destination Option          | Description                                                                                                                                                                 |
|:----------------------------|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| IngestionEndpointUri        | Ingest URL of ADX cluster created. Eg: `https://ingest-<clustername>.<region>.kusto.windows.net`.                                                                                                                                 |
| Database                    | The name of the database to which data should be ingested into.                                                                                         |
| TableName                   | The name of the table to which data should be ingested.                                                                                                                               |
| AuthenticationMode          | Authentication mode to be used by ADX target.                                                                                                                      |
| ApplicationClientId         | Application Client ID required for authentication.                                                                                                                     |
| ApplicationKey              | Application key required for authentication.                                                                                                                                                  |
| Authority                   | Tenant Id of the Azure Active Directory.                                                                                                                          |
| ManagedIdentityClientId     | In case of ManagedIdentity Authentication, this need to be set for user assigned identity.                                                                                 |
| FlushImmediately            | In case queued ingestion is selected, this property determines if is needed to flush the data immediately to ADX cluster. Not recommended to enable for data with higher workloads. The default is false. |
| MappingNameRef              | Use a data mapping configured in ADX.                                                                                        |


| Payload Option              | Description                                                                                                                                                                 |
|:----------------------------|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Layout                      | Formatting of the ADX-event FormattedMessage. The default is: `${logger}${message}`                                                                                         |
| IncludeEventProperties      | Include LogEvent Properties in the ADX-event. The default is true                                                                                                           |
| ContextProperty             | Include additional ContextProperties in the ADX-event. The default is empty collection.                                                                                           |
| BatchSize                   | Gets or sets the number of log events that should be processed in a batch by the lazy writer thread. (Default 1)                                                            |
| TaskDelayMilliseconds       | How many milliseconds to delay the actual send payload operation to optimize for batching (Default 1 ms)                                                                    |
| QueueLimit                  | Gets or sets the limit on the number of requests in the lazy writer thread request queue (Default 10000)                                                                    |
| OverflowAction              | Gets or sets the action (Discard, Grow, Block) to be taken when the lazy writer thread request queue count exceeds the set limit (Default Discard)                          |

### Mapping

Azure Data Explorer provides data mapping capabilities, allowing the ability to extract data rom the ingested JSONs as part of the ingestion. This allows paying a one-time cost of processing the JSON during ingestion, and reduced cost at query time.

By default, the sink uses the following data mapping:

| Column Name | Column Type | JSON Path    |
|-------------|-------------|--------------|
| Timestamp   | datetime    | $.Timestamp  |
| Level       | string      | $.Level      |
| Message     | string      | $.Message    |
| FormattedMessage | dynamic | $.FormattedMessage |
| Exception   | string      | $.Exception  |
| Properties  | dynamic     | $.Properties |

This mapping can be overridden using the following options:

* MappingNameRef: Use a data mapping configured in ADX.

### Authentication

Authentication can be used by setting the `AuthenticationMode` property in the nlog target configuration.

```xml
AuthenticationMode="<authentication_method_name>"
```

The `authentication_method_name` can be replaced with the following supported authentication methods:

1. `AadApplicationKey`
    * This is the *default* authentication mode. This requires the following properties to be set in the nlog target configuration.
        * `ApplicationClientId`
        * `ApplicationKey`
        * `Authority`
2. `AadUserManagedIdentity`
    * This authentication mode can be of two types System Assigned Managed Identity and User Assigned Managed Identity. In case of User Assigned Managed Identity, it requires the following properties to be set in the nlog target configuration:
        * `ManagedIdentityClientId`

### Running tests

To run the tests locally, you need to have an ADX cluster created.

1. Export environment variables for the following:

    * For Windows:

      ```powershell
        $env:INGEST_ENDPOINT="<ingestionURI>"
        $env:APP_ID="<appId>"
        $env:APP_KEY="<appKey>"
        $env:AZURE_TENANT_ID="<tenant>"
        $env:DATABASE="<databaseName>"
      ```

    * For Mac/Linux:

      ```bash
        export INGEST_ENDPOINT="<ingestionURI>"
        export APP_ID="<appId>"
        export APP_KEY="<appKey>"
        export AZURE_TENANT_ID="<tenant>"
        export DATABASE="<databaseName>"
      ```

2. Run the tests using the following command:

    ```bash
      dotnet test
    ```
