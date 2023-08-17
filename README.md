# NLog.Azure.Kusto

An Azure Data Explorer(ADX) custom target that writes log events to an [Azure Data Explorer (Kusto)](https://docs.microsoft.com/en-us/azure/data-explorer) cluster.

**Package** - [NLog.Azure.Kusto](http://nuget.org/packages/nlog.azure.kusto)
| **Platforms** - .Net 6.0

> You can now use the Kusto NLog connector with [_**free Kusto cluster**_](https://learn.microsoft.com/azure/data-explorer/start-for-free-web-ui) and [_**Microsoft Fabric**_](https://www.microsoft.com/microsoft-fabric) cluster URLs by providing the cluster URL in the `ConnectionString` parameter of the `ADXTarget` configuration.

## ****!! BREAKING CHANGE !!****
**IngestionEndpoint** will not be supported from verison 2.0.0. It has been replaced with Connection String based authentication. With Connection String based authentication, you can use different modes of authentication, such as User Prompt Authentication, User Token Authentication, and more. To learn more about Kusto connection strings and the authentication modes they support, please visit [Kusto connection strings.](https://learn.microsoft.com/azure/data-explorer/kusto/api/connection-strings/kusto)

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
      ConnectionString="<ADX connection string>"
      Database="<ADX database name>"
      TableName="<ADX table name>">
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
| ConnectionString            | Kusto connection string. Eg: `Data Source=https://ingest-<clustername>.<region>.kusto.windows.net;Database=NetDefaultDB;Fed=True`. Read about [Kusto Connection String](https://learn.microsoft.com/azure/data-explorer/kusto/api/connection-strings/kusto)                                          |
| Database                    | The name of the database to which data should be ingested into.                                                                                         |
| TableName                   | The name of the table to which data should be ingested.                                                                                                                               |
| ManagedIdentityClientId     | In case of ManagedIdentity Authentication, this need to be set for user assigned identity ("system" = SystemManagedIdentity)                                                |
| AzCliAuth              | Enable ADX connector to use Azure CLI authentication                                                                                 |
| FlushImmediately            | In case queued ingestion is selected, this property determines if is needed to flush the data immediately to ADX cluster. Not recommended to enable for data with higher workloads. The default is false. |
| MappingNameRef              | Use a data mapping configured in ADX.                                                                                        |
| ApplicationName             | Override default application-name                                                                                                                                           |
| ApplicationVersion          | Override default application-version                                                                                                                                        |


| Payload Option              | Description                                                                                                                                                                 |
|:----------------------------|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Layout                      | Formatting of the ADX-event FormattedMessage. The default is: `${logger}${message}`                                                                                         |
| IncludeEventProperties      | Include LogEvent Properties in the ADX-event. The default is true                                                                                                           |
| ContextProperty             | Include additional ContextProperties in the ADX-event. The default is empty collection.                                                                                           |
| BatchSize                   | Gets or sets the number of log events that should be processed in a batch by the lazy writer thread. (Default 1)                                                            |
| TaskDelayMilliseconds       | How many milliseconds to delay the actual send payload operation to optimize for batching (Default 1 ms)                                                                    |
| QueueLimit                  | Gets or sets the limit on the number of requests in the lazy writer thread request queue (Default 10000)                                                                    |
| OverflowAction              | Gets or sets the action (Discard, Grow, Block) to be taken when the lazy writer thread request queue count exceeds the set limit (Default Discard)                          |


> `IngestionEndpointUri` is longer supported with Version 2.0.0, as it has been replaced by Connection String based authentication. Read more about [Kusto connection strings](https://learn.microsoft.com/azure/data-explorer/kusto/api/connection-strings/kusto)

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

Authentication will be taken according to the kusto connection string passed in the nlog target configuration.

There are few cases to keep in mind for the following authentication modes:
1. `Managed Identity Authentication`
    * This authentication mode can be of two types System Assigned Managed Identity and User Assigned Managed Identity. In case of User Assigned Managed Identity, it requires the following properties to be set in the nlog target configuration::
        * `ManagedIdentityClientId` :
            * `system` : This will enable managed identity authentication for system assigned managed identity.
            * `<clientId>`:  Setting `ManagedIdentityClientId` to a specific clientId will enable managed identity authentication for user assigned managed identity.
2. `AzCliAuth`
    * This authentication mode will use the Azure CLI to authenticate. This authentication mode will only work when the application is running on a machine with Azure CLI installed and logged in. Accepted values `true` or `false`. Default value is `false`.

### Running tests

To run the tests locally, you need to have an ADX cluster created.

1. Export environment variables for the following:

    * For Windows:

      ```powershell
        $env:CONNECTION_STRING="<connectionString>"
        $env:DATABASE="<databaseName>"
      ```

    * For Mac/Linux:

      ```bash
        export CONNECTION_STRING="<connectionString>"
        export DATABASE="<databaseName>"
      ```

2. Run the tests using the following command:

    ```bash
      dotnet test
    ```
