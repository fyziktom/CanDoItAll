
# Command Sequence

| Order | Command or action | Purpose | Must pass before |
| --- | --- | --- | --- |
| 1 | dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj | Compile the changed solution entrypoint and fail fast on missing registrations, migrations, or component build errors. | Before any phase is marked closed. |
| 2 | dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Storage|FullyQualifiedName~Routing|FullyQualifiedName~Recommendation|FullyQualifiedName~ManagedFiles|FullyQualifiedName~LocalFileOpener" | Run focused unit coverage for storage contracts, path safety, compatibility, routing, and capability gating. | Before Phase 03 closes. |
| 3 | dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~Storage|FullyQualifiedName~ManagedFiles|FullyQualifiedName~Snapshot|FullyQualifiedName~Ipfs|FullyQualifiedName~BatchTransfer|FullyQualifiedName~ProfileHarness" | Run focused integration coverage for provider flows and access endpoints. | Before Phase 03 closes. |
| 4 | dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~StorageDriver|FullyQualifiedName~StorageSettings|FullyQualifiedName~ProjectStructureArtifact|FullyQualifiedName~PromptFactory" | Run automated browser coverage with saved screenshots. | Before Phase 03 closes and again before final closure. |
| 5 | Playwright MCP manual pass in headed browser at 1900x1200 | Validate the changed UI surfaces visually with screenshots and interaction logs. | Before Phase 04 closes. |
| 6 | Playwright MCP manual pass at 1366x900 or similar narrower width | Catch overflow, clipping, collapsing actions, and modal/layout issues after the desktop pass. | Before Phase 04 closes. |
| 7 | Optional mtp-hot-reload iteration loop | Use only for faster local debugging because the repo uses Microsoft.NET.Test.Sdk; never treat it as final proof. | Allowed only during active fixing; finish with clean non-hot-reload runs. |

## Notes

- The repo exposes `CanDoItAll.slnx`, but the bundle keeps project-level `dotnet build` and `dotnet test` commands for tighter phase proof.
- The relevant test projects use `Microsoft.NET.Test.Sdk`; optional `mtp-hot-reload` can accelerate local iteration but never replaces final clean runs.
- Manual Playwright MCP actions must be logged in the execution report with screenshot paths and written findings.

