---
description: Upgrade NuGet dependencies for NLog.Azure.Kusto. Checks for outdated packages, updates Directory.Packages.props, restores, builds, runs unit tests, and bumps the connector version across all required files.
---

You are upgrading NuGet dependencies for the `NLog.Azure.Kusto` repo and performing the associated connector version bump. Follow every phase in order. Do not skip any phase.

## Phase 0 — Create a Branch

Before making ANY changes, create a new branch from the latest `main`:

```powershell
git checkout main
git pull origin main
git checkout -b upgrade-deps/<short-description>
```

All changes in the subsequent phases must be made on this branch — never commit directly to `main`.

---

## Critical Rules

- Package versions are managed in **one file only**: `src/Directory.Packages.props` (Central Package Management). Never add `Version` to `.csproj` files.
- **Do NOT run E2E tests** (`ADXSinkE2ETest`). They require a live ADX cluster with `CONNECTION_STRING` and `DATABASE` env vars not available locally.
- `xunit.runner.visualstudio` v3+ is compatible with xunit v2.x tests — these do not need to be at the same major version, but should both be upgraded when outdated.
- A dependency upgrade **always requires a MINOR version bump** to the connector (`X.Y.Z` → `X.(Y+1).0`).

---

## Phase 1 — Discover Outdated Packages

```powershell
cd src
dotnet list package --outdated
```

---

## Phase 2 — Assess Risk

Before changing anything, classify each outdated package:

| Package | Risk | Key APIs to check if upgrading |
|---|---|---|
| `Microsoft.Azure.Kusto.Ingest` | HIGH | `ADXTarget.cs` + `ADXSinkOptions.cs`: `KustoIngestFactory`, `IKustoIngestClient`, `KustoIngestionProperties`, `KustoQueuedIngestionProperties`, `StreamSourceOptions`, `DataSourceFormat`, `IngestionMapping` |
| `NLog` | HIGH | `ADXTarget.cs`: `AsyncTaskTarget`, `JsonLayout`, `JsonAttribute`, `Layout<T>`, `RenderLogEvent`, `IncludeEventProperties`, `ContextProperties` |
| `xunit` | MODERATE | `xunit.runner.visualstudio` v3+ supports xunit v2.x tests. Upgrade both when outdated but they don't need matching major versions. |
| `xunit.runner.visualstudio` | MODERATE | v3+ is compatible with xunit v2.x. Check test runner output after upgrading. |
| `Microsoft.NET.Test.Sdk` | LOW | Safe to upgrade independently. |
| `coverlet.collector` | LOW | Safe to upgrade independently. |

For HIGH-risk packages: read the current source before upgrading so you can spot API breakage after the upgrade.

---

## Phase 3 — Update `src/Directory.Packages.props`

Edit only this file. Update the `Version` attribute for each package being upgraded.

---

## Phase 4 — Restore

```powershell
dotnet restore src/NLog.Azure.Kusto.sln
```

If restore fails: revert, report the conflict, stop.

---

## Phase 5 — Build

```powershell
dotnet build src/NLog.Azure.Kusto.sln
```

If build fails due to API changes, fix the breakage in source before proceeding. Common files: `ADXTarget.cs`, `ADXSinkOptions.cs`, `ADXLogEvent.cs`. Re-run build until it passes cleanly.

---

## Phase 6 — Unit Tests

```powershell
dotnet test src/NLog.Azure.Kusto.Tests/NLog.Azure.Kusto.Tests.csproj --filter "FullyQualifiedName~ADXTargetTest"
```

Fix any failures before proceeding.

---

## Phase 7 — Bump the Connector Version

Read the current version from `src/NLog.Azure.Kusto/NLog.Azure.Kusto.csproj`. Compute new version: `X.(Y+1).0`.

Update all 7 locations — no exceptions:

| File | What to change |
|---|---|
| `src/NLog.Azure.Kusto/NLog.Azure.Kusto.csproj` | `<Version>X.Y.Z</Version>` |
| `src/NLog.Azure.Kusto/Properties/AssemblyInfo.cs` | `AssemblyVersion("X.Y.Z.0")` and `AssemblyFileVersion("X.Y.Z.0")` |
| `src/NLog.Azure.Kusto/ADXSinkOptions.cs` | `private const string ClientVersion = "X.Y.Z"` |
| `src/NLog.Azure.Kusto.Samples/NLog.Azure.Kusto.Samples.csproj` | `<Version>X.Y.Z</Version>` |
| `src/NLog.Azure.Kusto.Tests/NLog.Azure.Kusto.Tests.csproj` | `<Version>X.Y.Z</Version>` |
| `README.md` | `dotnet add package NLog.Azure.Kusto --version X.Y.Z` |
| `src/NLog.Azure.Kusto.Samples/README.md` | `dotnet add package NLog.Azure.Kusto --version X.Y.Z` |

After updating, do a final build to confirm nothing broke:

```powershell
dotnet build src/NLog.Azure.Kusto.sln
```

To verify no version locations were missed, search for the old version string:

```powershell
grep -r "OLD_VERSION" src/ README.md
```

---

## Phase 8 — Commit, Push, and Create Draft PR

Commit all changes with a descriptive message:

```powershell
git add -A
git commit -m "Upgrade NuGet dependencies and bump connector to X.Y.Z"
git push origin HEAD
```

Then create a **draft** pull request targeting `main`:

```powershell
gh pr create --base main --draft --title "Upgrade NuGet dependencies and bump connector to X.Y.Z" --body "## Changes\n- Upgraded: [list packages from/to]\n- Connector version: [old] → [new]\n- Build: PASS\n- Unit tests: PASS"
```

If `gh` CLI is not available, instruct the user to create the draft PR manually on GitHub.

**Never merge automatically. Always create as draft so a human reviews first.**

---

## Phase 9 — Report

Summarise:
- Which packages were upgraded and from/to which versions
- Which packages were skipped and why
- Any source code changes required and why
- The new connector version and confirmation all 7 files were updated
- Build and test outcome
- Link to the draft PR
