# Getting Started with Application Logging using Azure Data Explorer Target for NLog

This guide will help you in setting up and configuring the Azure Data Explorer Target for NLog using sample application. You will learn how to write log messages and view them in Azure Data Explorer(ADX) cluster.

## Prerequisites

- An ADX cluster and database. If you do not have one, [Create Azure Data Explorer Cluster and DB](https://docs.microsoft.com/en-us/azure/data-explorer/create-cluster-database-portal)
- An Active Directory Application with Database Ingestor role. If you do not have one, [Create Azure Active Directory App Registration and grant it permissions to DB](https://docs.microsoft.com/en-us/azure/kusto/management/access-control/how-to-provision-aad-app) (Save the application ID and key for later).
- Create a table in Azure Data Explorer to store logs. Following command can be used to create a table with the name "ADXNLogSample".

```sql
.create table ADXNLogSample (Timestamp:datetime, Level:string, Message:string, FormattedMessage:string, Exception:string, Properties:dynamic)
```

- Clone the NLog-ADX Target git repo
- Set the following environment variables in the sample application:
  - CLUSTER_URI : Ingest URL of ADX cluster created. Eg: `https://ingest-<clustername>.<region>.kusto.windows.net`
  - DATABASE : The name of the database to which data should be ingested into.
  - APP_ID : Application Client ID required for authentication.
  - APP_KEY : Application key required for authentication.
  - AZURE_TENANT_ID : Tenant Id of the Azure Active Directory.

## Step 1: Install the ADX Target for NLog

The ADX Target for NLog is available as a NuGet package. To install it, open the Package Manager Console and enter the following command:

```powershell
Install-Package NLog.Azure.Kusto
```

### Step 1.1: Configure the ADX Target for NLog

Alternatively, If you do not want to use environment variables, you can configure the `NLog.config` file.
Open the configuration file and replace the existing target configuration one with the following:

```xml
<target name="adxtarget" xsi:type="ADXTarget"
  IngestionEndpointUri="<ADX connection string>"
  Database="<ADX database name>"
  TableName="<ADX table name>"
  ApplicationClientId="<AAD App clientId>"
  ApplicationKey="<AAD App key>"
  Authority="<AAD tenant id>"
/>
```

## Step 2: Build the application and run it

- Open a Powershell window, navigate to NLog ADX Target base folder and run the following command

```powershell
    dotnet build
```

- Once build got completed, Navigate to src/Nlog.Azure.Kusto.Samples/ run the following command to run the sample application

```powershell
    dotnet run
```

- The Program.cs contains predefined logs which will start getting ingested to ADX.
- The ingested log data can be verified by querying the created log table(ADXNLogSample in our case) by using the following KQL command.

```sql
 ADXNLogSample | take 10
```
