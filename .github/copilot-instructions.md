# GitHub Copilot Instructions — NLog.Azure.Kusto

## What this repo is

A .NET library (NuGet package) that acts as a **custom NLog target**, routing application log events into **Azure Data Explorer (ADX/Kusto)**. It bridges the NLog logging framework to ADX's queued and streaming ingestion APIs.

Published package: `NLog.Azure.Kusto` — multi-targets `net8.0`, `netstandard2.0`, and `net472`.

---

## Repository structure

```
src/
├── Directory.Build.props               # Shared MSBuild settings for all projects
├── Directory.Packages.props            # Central NuGet version management (ALL versions live here)
├── NLog.Azure.Kusto/                   # Main library — what gets published to NuGet
│   ├── ADXTarget.cs                    # Core: custom NLog AsyncTaskTarget
│   ├── ADXSinkOptions.cs               # Config holder + builds Kusto connection strings
│   ├── ADXLogEvent.cs                  # Schema for each log record sent to ADX
│   └── AuthenticationType.cs           # Enum: auth modes (managed identity, workload, AzCLI, etc.)
├── NLog.Azure.Kusto.Samples/           # Example console app
│   ├── Program.cs                      # Shows Info/Warn/Error structured logging usage
│   └── NLog.config                     # XML config wiring NLog → ADXTarget
└── NLog.Azure.Kusto.Tests/
    ├── ADXTargetTest.cs                # Unit tests (config loading, validation)
    ├── ADXSinkE2ETest.cs               # E2E tests (requires real ADX cluster + env vars)
    └── config/
        ├── adxtarget.config            # Valid NLog XML config for unit tests
        └── adxtargeterror.config       # Invalid config for error-path tests
```

---

## Dependency management — CRITICAL

This repo uses **Central Package Management (CPM)**. All NuGet package versions are defined **only** in one file:

**`src/Directory.Packages.props`**

```xml
<PackageVersion Include="Microsoft.Azure.Kusto.Ingest" Version="14.1.0" />
<PackageVersion Include="NLog" Version="6.1.1" />
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.3.0" />
<PackageVersion Include="xunit" Version="2.9.3" />
<PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
<PackageVersion Include="coverlet.collector" Version="8.0.0" />
```

Individual `.csproj` files reference packages **without versions** — the version comes from `Directory.Packages.props`:
```xml
<PackageReference Include="Microsoft.Azure.Kusto.Ingest" />
<PackageReference Include="NLog" />
```

### How to upgrade a dependency

1. Edit **only** `src/Directory.Packages.props` — change the `Version` attribute for the target package.
2. Run `dotnet restore` from the `src/` folder to pull the new version.
3. Run `dotnet build src/NLog.Azure.Kusto.sln` to verify no breaking changes or new warnings.
4. Run `dotnet test src/NLog.Azure.Kusto.Tests/NLog.Azure.Kusto.Tests.csproj --filter "FullyQualifiedName~ADXTargetTest"` to run unit tests (E2E tests require a live ADX cluster).
5. If tests fail due to behavioral changes in upgraded packages, update test expectations accordingly.
6. If this is a library release, bump the version in **all** of these locations:
   - `src/NLog.Azure.Kusto/NLog.Azure.Kusto.csproj` → `<Version>`
   - `src/NLog.Azure.Kusto/Properties/AssemblyInfo.cs` → `AssemblyVersion` and `AssemblyFileVersion`
   - `src/NLog.Azure.Kusto/ADXSinkOptions.cs` → `ClientVersion` constant
   - `src/NLog.Azure.Kusto.Tests/NLog.Azure.Kusto.Tests.csproj` → `<Version>`
   - `src/NLog.Azure.Kusto.Samples/NLog.Azure.Kusto.Samples.csproj` → `<Version>`
   - `README.md` → install command version
   - `src/NLog.Azure.Kusto.Samples/README.md` → install command version

### Checking for outdated packages

```powershell
cd src
dotnet list package --outdated
```

### Key upgrade considerations

| Package | Notes on upgrading |
|---|---|
| `Microsoft.Azure.Kusto.Ingest` | Check `KustoIngestFactory`, `IKustoIngestClient`, `KustoIngestionProperties`, `KustoQueuedIngestionProperties` APIs — these are used in `ADXTarget.cs` |
| `NLog` | **Major version upgrades may introduce breaking changes.** Check `AsyncTaskTarget`, `JsonLayout`, `Layout<T>`, `RenderLogEvent` APIs. NLog 6.x deprecated `[RequiredParameter]` — validation now happens in `InitializeTarget()` via `ArgumentNullException`. Check for new obsolete warnings after upgrading. |
| `xunit` / `xunit.runner.visualstudio` | `xunit.runner.visualstudio` v3+ is compatible with xunit v2.x tests. These do not need to be at the same major version. |
| `Microsoft.NET.Test.Sdk` | Usually safe to upgrade independently |
| `coverlet.collector` | Only impacts code coverage collection, safe to upgrade |

---

## Build commands

```powershell
# Restore packages
dotnet restore src/NLog.Azure.Kusto.sln

# Build everything
dotnet build src/NLog.Azure.Kusto.sln

# Run unit tests only (no ADX cluster needed)
dotnet test src/NLog.Azure.Kusto.Tests/NLog.Azure.Kusto.Tests.csproj --filter "FullyQualifiedName~ADXTargetTest"

# Run all tests (requires CONNECTION_STRING and DATABASE env vars for E2E)
dotnet test src/NLog.Azure.Kusto.Tests/NLog.Azure.Kusto.Tests.csproj
```

---

## Key classes and how they interact

### `ADXTarget` (inherits `AsyncTaskTarget`)
The NLog custom target. NLog calls `WriteAsyncTask(IList<LogEventInfo>)` with batches of log events.

Flow:
1. `InitializeTarget()` — reads NLog XML config, builds `ADXSinkOptions`, creates `IKustoIngestClient`
2. `WriteAsyncTask()` — serializes batch → `ADXLogEvent` JSON → GZip stream → ingests to ADX
3. `CreateStreamFromLogEvents()` — renders each event using NLog layouts, creates compressed stream
4. `IngestFromStreamAsync()` — calls either streaming or queued ingestion API

### `ADXSinkOptions`
Internal config holder. Two important methods:
- `GetIngestKcsb()` — builds connection string for DM endpoint (queued ingestion); auto-prepends `ingest-`
- `GetEngineKcsb()` — builds connection string for engine endpoint (streaming ingestion)

### `ADXLogEvent`
Schema written to ADX per log record:
| Field | Type | Source |
|---|---|---|
| `Timestamp` | `DateTime` | `logEventInfo.TimeStamp` (UTC) |
| `Level` | `string` | `logEventInfo.Level` |
| `Message` | `string` | Raw message template |
| `FormattedMessage` | `string` | NLog-rendered message |
| `Exception` | `string` | `exception.ToString()` |
| `Properties` | `dynamic` (JSON object) | NLog event properties |

### `AuthenticationType` enum
Supported auth modes:
- `None` — auth embedded in connection string
- `AadUserManagedIdentity` — user-assigned managed identity
- `AadSystemManagedIdentity` — system-assigned managed identity
- `AadWorkloadIdentity` — Kubernetes workloads (`WorkloadIdentityCredential`)
- `AadUserPrompt` — AzCLI + interactive browser fallback
- `AddAzCli` — Azure CLI auth

---

## Ingestion modes

| Mode | Config | Latency | Throughput |
|---|---|---|---|
| **Queued** (default) | `UseStreamingIngestion="false"` | ~5 min | Very high |
| **Streaming** | `UseStreamingIngestion="true"` | Seconds | Lower |

Queued uses `KustoIngestFactory.CreateQueuedIngestClient(dmKcsb)`.  
Streaming uses `KustoIngestFactory.CreateManagedStreamingIngestClient(engineKcsb, dmKcsb)`.

---

## NLog XML configuration pattern

```xml
<nlog>
  <extensions>
    <add assembly="NLog.Azure.Kusto"/>
  </extensions>
  <targets>
    <target name="adxtarget" xsi:type="ADXTarget"
      ConnectionString="<kusto connection string>"
      Database="<database name>"
      TableName="<table name>"
      UseStreamingIngestion="false"
      FlushImmediately="false"
      MappingNameRef=""
      AuthenticationType="None">
        <contextproperty name="HostName" layout="${hostname}" />
    </target>
  </targets>
  <rules>
    <logger minlevel="Info" name="*" writeTo="adxtarget"/>
  </rules>
</nlog>
```

---

## E2E test setup

The E2E tests in `ADXSinkE2ETest.cs` require two environment variables:

```powershell
$env:CONNECTION_STRING = "Data Source=https://<cluster>.<region>.kusto.windows.net;Fed=True"
$env:DATABASE = "<database-name>"
```

The tests create a temporary table, ingest logs, query back with retries, and drop the table on cleanup.

---

## Assembly signing

The main library assembly is strong-name signed using `NLog.Azure.Kusto.snk`. Do not remove or replace this file. The signing is configured in `NLog.Azure.Kusto.csproj`:
```xml
<SignAssembly>true</SignAssembly>
<AssemblyOriginatorKeyFile>NLog.Azure.Kusto.snk</AssemblyOriginatorKeyFile>
```

---

## Common tasks

### "Upgrade all packages to latest"
1. Run `dotnet list package --outdated` from `src/`
2. For each outdated package, update its `Version` in `src/Directory.Packages.props`
3. Run `dotnet restore src/NLog.Azure.Kusto.sln`
4. Run `dotnet build src/NLog.Azure.Kusto.sln` — check for new warnings (especially obsolete API warnings)
5. Run unit tests: `dotnet test --filter "FullyQualifiedName~ADXTargetTest"` — if tests fail, check for behavioral changes in upgraded packages and update test expectations
6. Fix any new build warnings introduced by the upgrade (e.g., removing deprecated attributes)
7. Bump library version in all locations listed under "How to upgrade a dependency" step 6

### "Add a new NuGet package"
1. Add a `<PackageVersion Include="PackageName" Version="x.y.z" />` entry to `src/Directory.Packages.props`
2. Add a `<PackageReference Include="PackageName" />` (no version) to the relevant `.csproj`
3. Run `dotnet restore`

### "What version of X is being used?"
Check `src/Directory.Packages.props` — all versions are defined there.
