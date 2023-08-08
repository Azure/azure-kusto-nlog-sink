# NLog.Azure.Kusto

An Azure Data Explorer(ADX) custom target that writes log events to an [Azure Data Explorer (Kusto)](https://docs.microsoft.com/en-us/azure/data-explorer) cluster.

**Package** - [NLog.Azure.Kusto](http://nuget.org/packages/nlog.azure.kusto)
| **Platforms** - .Net 6.0

## ****!! BREAKING CHANGE !!****
**IngestionEndpoint** will not be supported from verison 2.0.0 and above and will support only Connection String based authentication. Read more about [Kusto connection strings.](https://learn.microsoft.com/azure/data-explorer/kusto/api/connection-strings/kusto)

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
    <target name="adxtarget" xsi:type="ADXTarget"
      ConnectionString="<ADX connection string>"
      Database="<ADX database name>"
      TableName="<ADX table name>"
    />
  </targets>
  <rules>
    <logger minlevel="Info" name="*" writeTo="adxtarget"/>
  </rules>
</nlog>
```

## Available Configuration Options

| Option                      | Description                                                                                                                                                                 |
|-----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ConnectionString                   | Connection String for ADX cluster. Read more about [Kusto connection string](https://learn.microsoft.com/azure/data-explorer/kusto/api/connection-strings/kusto)                                                                                                                                 |
| Database                       | The name of the database to which data should be ingested into.                                                                                         |
| TableName                     | The name of the table to which data should be ingested.                                                                                                                               |
| ManagedIdentityClientId              | In case of ManagedIdentity Authentication, this need to be set for user assigned identity.                                                                                 |
| FlushImmediately              | In case queued ingestion is selected, this property determines if is needed to flush the data immediately to ADX cluster. Not recommended to enable for data with higher workloads. The default is false.                                                                          |
| MappingNameRef      | Use a data mapping configured in ADX.                                                                                        |
| ColumnsMapping | Use an ingestion-time data mapping.                                                                                                                              |
| IngestionTimout                 | Set timeout for ingestions in seconds.                               |

### Mapping

Azure Data Explorer provides data mapping capabilities, allowing the ability to extract data rom the ingested JSONs as part of the ingestion. This allows paying a one-time cost of processing the JSON during ingestion, and reduced cost at query time.

By default, the sink uses the following data mapping:

| Column Name | Column Type | JSON Path    |
|-------------|-------------|--------------|
| Timestamp   | datetime    | $.Timestamp  |
| Level       | string      | $.Level      |
| Message     | string      | $.Message    |
| FormattedMessage     | dynamic      | $.FormattedMessage    |
| Exception   | string      | $.Exception  |
| Properties  | dynamic     | $.Properties |

This mapping can be overridden using the following options:

* MappingNameRef: Use a data mapping configured in ADX.
* ColumnsMapping: Use an ingestion-time data mapping.

### Authentication

Authentication will be taken according to the kusto connection string passed in the nlog target configuration.

There are few cases to keep in mind for the following authentication modes:
1. `ManagedIdentity`
    * This authentication mode can be of two types System Assigned Managed Identity and User Assigned Managed Identity. In case of User Assigned Managed Identity, it requires the following properties to be set in the nlog target configuration::
        * `ManagedIdentityClientId` :
            * `system` : This will enable managed identity authentication for system assigned managed identity.
            * `<clientId>`:  Setting `ManagedIdentityClientId` to a specific clientId will enable managed identity authentication for user assigned managed identity.

### Running tests

To run the tests locally, you need to have an ADX cluster created.

1. Export environment variables for the following:

    * For Windows:

      ```powershell
        $env:CONNECTION_STRING="<connectionstring>"
        $env:DATABASE="<databaseName>"
      ```

    * For Mac/Linux:

      ```bash
        export CONNECTION_STRING="<connectionstring>"
        export DATABASE="<databaseName>"
      ```

2. Run the tests using the following command:

    ```bash
      dotnet test
    ```
