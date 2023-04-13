# NLog.Azure.Kusto

An Azure Data Explorer(ADX) custom target that writes log events to an [Azure Data Explorer (Kusto)](https://docs.microsoft.com/en-us/azure/data-explorer) cluster.

**Package** - [NLog.Azure.Kusto](http://nuget.org/packages/nlog.azure.kusto)
| **Platforms** - .Net 6.0

## Getting started

Install from [NuGet](https://nuget.org/packages/serilog.sinks.azuredataexplorer):

```powershell
Install-Package Serilog.Sinks.AzureDataExplorer
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
      IngestionEndpointUri="<ADX connection string>"
      Database="<ADX database name>"
      TableName="<ADX table name>"
      ApplicationClientId="<AAD App clientId>"
      ApplicationKey="<AAD App key>"
     Authority="<AAD tenant id>"
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
| IngestionEndpointUri                   | Ingest URL of ADX cluster created. Eg: `https://ingest-<clustername>.<region>.kusto.windows.net`.                                                                                                                                 |
| Database                       | The name of the database to which data should be ingested into.                                                                                         |
| TableName                     | The name of the table to which data should be ingested.                                                                                                                               |
| AuthenticationMode                      | Authentication mode to be used by ADX target.                                                                                                                      |
| ApplicationClientId                  | Application Client ID required for authentication.                                                                                                                     |
| ApplicationKey                       | Application key required for authentication.                                                                                                                                                  |
| Authority              | Tenant Id of the Azure Active Directory.                                                                                                                          |
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
| FormattedMessage     | string      | $.FormattedMessage    |
| Exception   | string      | $.Exception  |
| Properties  | dynamic     | $.Properties |

This mapping can be overridden using the following options:

* MappingNameRef: Use a data mapping configured in ADX.
* ColumnsMapping: Use an ingestion-time data mapping.

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

