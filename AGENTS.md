# AGENTS.md

> This file provides guidance for AI agents (GitHub Copilot, Copilot Workspace, etc.) working on this repository.

## Project Overview

**NLog.Azure.Kusto** is an NLog custom target that writes log events to [Azure Data Explorer (Kusto)](https://learn.microsoft.com/azure/data-explorer). It is published as the NuGet package [`NLog.Azure.Kusto`](https://www.nuget.org/packages/NLog.Azure.Kusto).

- **Language:** C#
- **Framework:** .NET 8.0, .NET Standard 2.0, .NET Framework 4.7.2 (multi-target)
- **Logging framework:** [NLog](https://nlog-project.org/) (extends `AsyncTaskTarget`)
- **Ingestion SDK:** [Microsoft.Azure.Kusto.Ingest](https://www.nuget.org/packages/Microsoft.Azure.Kusto.Ingest)
- **Testing framework:** [xUnit](https://xunit.net/)
- **License:** MIT
- **Owner:** Microsoft / Azure

## Repository Structure

```
├── AGENTS.md                          # AI agent guidance (this file)
├── README.md                          # User-facing documentation
├── CODE_OF_CONDUCT.md
├── LICENSE
├── SECURITY.md
├── SUPPORT.md
├── .github/
│   └── workflows/
│       ├── dotnet.yml                 # CI: build + test on push/PR to main
│       └── nuget.yml                  # Manual NuGet publish workflow
└── src/
    ├── Directory.Build.props          # Shared MSBuild properties (all projects)
    ├── Directory.Packages.props       # Centralized NuGet package versions
    ├── NLog.Azure.Kusto.sln           # Solution file
    ├── NLog.Azure.Kusto/              # Main library project
    │   ├── NLog.Azure.Kusto.csproj
    │   ├── ADXTarget.cs               # Core NLog target implementation
    │   ├── ADXSinkOptions.cs          # Configuration & connection management
    │   ├── ADXLogEvent.cs             # Log event model for Kusto ingestion
    │   ├── AuthenticationType.cs      # Authentication mode enum
    │   └── AssemblyInfo.cs
    ├── NLog.Azure.Kusto.Tests/        # Unit & E2E tests
    │   ├── NLog.Azure.Kusto.Tests.csproj
    │   ├── ADXTargetTest.cs           # Unit tests (config loading)
    │   ├── ADXSinkE2ETest.cs          # E2E tests (requires live ADX cluster)
    │   ├── Usings.cs
    │   └── config/                    # NLog test configuration files
    └── NLog.Azure.Kusto.Samples/      # Sample console application
        ├── NLog.Azure.Kusto.Samples.csproj
        ├── Program.cs
        └── NLog.config
```

## Build & Test

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Build

```bash
dotnet restore src
dotnet build src -c Release
```

### Run Unit Tests

```bash
dotnet test src -c Release
```

### Run E2E Tests (requires live ADX cluster)

E2E tests need environment variables pointing to an Azure Data Explorer cluster:

```bash
# Linux / macOS
export CONNECTION_STRING="<your-kusto-connection-string>"
export DATABASE="<your-database-name>"

# Windows PowerShell
$env:CONNECTION_STRING="<your-kusto-connection-string>"
$env:DATABASE="<your-database-name>"
```

Then run:

```bash
dotnet test src -c Release --verbosity normal
```

### Run the Sample

```bash
dotnet run --project src/NLog.Azure.Kusto.Samples
```

## Dependency Management

This project uses **centralized NuGet package version management**. All package versions are defined in:

```
src/Directory.Packages.props
```

Individual `.csproj` files reference packages **without** specifying a version — versions come from `Directory.Packages.props`. When updating a dependency, always update it in `Directory.Packages.props`, not in individual `.csproj` files.

### Key Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Azure.Kusto.Ingest` | Azure Data Explorer ingestion client |
| `NLog` | NLog logging framework |
| `Microsoft.NET.Test.Sdk` | .NET test platform SDK |
| `xunit` | xUnit testing framework |
| `xunit.runner.visualstudio` | xUnit VS Test adapter |
| `coverlet.collector` | Code coverage collection |

## Contribution Workflow

### Branching

1. **Always pull the latest `main`** before creating a new branch:

   ```bash
   git checkout main
   git pull origin main
   ```

2. **Create your feature branch from `main`** using the naming convention:

   | Branch Type | Pattern | Example |
   |-------------|---------|---------|
   | Feature | `feature/<short-description>` | `feature/add-retry-policy` |
   | Bug fix | `bugfix/<short-description>` | `bugfix/connection-timeout` |
   | Chore | `chore/<short-description>` | `chore/update-dependencies` |
   | Release | `release/v<version>` | `release/v3.1.0` |
   | Docs | `docsfix` or `docs/<description>` | `docs/update-readme` |

   ```bash
   git checkout -b feature/my-feature-name
   ```

### Commit Messages

Write clear, descriptive commit messages. Follow these conventions:

- Use **imperative mood** or a concise description of the change
- Reference the PR number when applicable (GitHub adds this on squash-merge)
- Keep the subject line under 72 characters

**Examples of good commit messages:**

```
Add retry policy for transient ingestion errors
Update Microsoft.Azure.Kusto.Ingest to ver. 14.1.0
Fix connection string visibility issue
ADXSinkE2ETest - increase timeout for flaky tests
```

### Pull Request Titles

PR titles follow the same convention as commit messages — they should be clear and descriptive since PRs are squash-merged and the PR title becomes the commit message on `main`.

**Examples of good PR titles:**

```
Microsoft.Azure.Kusto.Ingest ver. 14.1.0
Support for AuthenticationType = AadWorkloadIdentity
Fix connection timeout on batch ingestion
Update test dependencies to latest stable versions
```

### Pull Request Guidelines

- Target the `main` branch
- Ensure CI passes (build + tests)
- Provide a clear description of the change and why it is needed
- Link related issues if applicable

## CI/CD

### Build & Test (`dotnet.yml`)

- **Triggers:** Push to `main`, PRs targeting `main`
- **Runs on:** `ubuntu-latest`
- **Timeout:** 15 minutes
- **Steps:** Restore → Build (Release) → Test (with ADX E2E environment)
- **Environment:** `e2e` (provides `CONNECTION_STRING` and `DATABASE` vars)

### NuGet Publish (`nuget.yml`)

- **Trigger:** Manual (`workflow_dispatch`)
- **Steps:** Restore → Build → Pack → Push to NuGet.org
- **Secret:** `NUGET_API_KEY`

## Architecture Notes

### Core Pattern

`ADXTarget` extends NLog's `AsyncTaskTarget`, which provides:

- Async batching of log events
- Configurable batch size, queue limits, and overflow behavior
- Thread-safe lazy writer pattern

### Ingestion Modes

The target supports two ingestion modes:

1. **Streaming ingestion** — Low latency, logs sent immediately via `IKustoIngestClient`
2. **Queued ingestion** — Higher throughput, logs batched and queued via `IKustoQueuedIngestClient`

### Authentication

Authentication is configured via `AuthenticationType` enum and `KustoConnectionStringBuilder`. Supported modes:

- Connection string (default)
- AAD User Managed Identity
- AAD System Managed Identity
- AAD Workload Identity (Kubernetes)
- Azure CLI
- User Prompt (dev only)

### Data Flow

```
LogEvent → ADXLogEvent (JSON) → GZip compress → MemoryStream → Kusto Ingest Client → ADX Table
```

Key implementation details:

- Uses `RecyclableMemoryStreamManager` for efficient memory management
- JSON serialization with custom `UnsafeRawJsonConverter` for properties
- Automatic retry with skip for authentication and permanent errors
- Connection string translation between engine and ingest endpoints
