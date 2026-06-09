# SB009 Proof Manifest

Status: `Completed`

## Changed File Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/ApplicationStartupIntegrationTests.cs` | `5A7D037D25BE0D6DD101C78A22A5F0B94AFB0E8461C5A3E28A13FAFF6808389E` |

## Command Transcripts

| Proof | Transcript | Result |
| --- | --- | --- |
| SB007 startup/composition source inventory | `bundle://proof/SB007/transcripts/startup-composition-inventory.txt` | Passed |
| SB008 app-start smoke source assertions | `bundle://proof/SB009/transcripts/startup-smoke-source-assertions.txt` | Passed |
| Focused deterministic app-start smoke test | `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt` | Passed |
| Web project build | `bundle://proof/SB009/transcripts/web-project-build.txt` | Passed |
| Forbidden drift scan | `bundle://proof/SB009/transcripts/forbidden-drift-scan.txt` | Passed |
| Anti-stub audit | `bundle://proof/SB009/transcripts/anti-stub-audit-startup-test.txt` | Passed |
| Semantic positive source audit | `bundle://proof/SB009/transcripts/semantic-positive-source-audit.txt` | Passed |
| Changed file and transcript hashes | `bundle://proof/SB009/transcripts/changed-file-hashes.txt` | Passed |
| Prepared-stage bundle validator after SB009 | `bundle://proof/SB009/transcripts/prepared-validator-after-sb009.txt` | Passed |

## Transcript Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://proof/SB007/transcripts/startup-composition-inventory.txt` | `19E9441F0986476D149318D4953CFFB5B076CDE93F1F3F113749ABB07FB981AD` |
| `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt` | `3F34C560B831A160CEBCC741305D7E22A29AA66267602785B6696312F2D855A0` |
| `bundle://proof/SB009/transcripts/web-project-build.txt` | `A339A5C519BF6AAAE5F16CF57C2250BE506C8F81D28402A5B463B2111D586129` |
| `bundle://proof/SB009/transcripts/startup-smoke-source-assertions.txt` | `1CE10FA368F4B043571F1072DB0CDA1075C7904F4BA56C31F3EA84DA3A403799` |
| `bundle://proof/SB009/transcripts/forbidden-drift-scan.txt` | `003B384D0C6E39F5D4A04B75318FD66077AC9F613C7A92BEE4609108E022D596` |
| `bundle://proof/SB009/transcripts/anti-stub-audit-startup-test.txt` | `F620A98FD94CAEBA52961E882AE1A3E62D422E3C33B03527AAC5DC7DC9ECA73D` |
| `bundle://proof/SB009/transcripts/semantic-positive-source-audit.txt` | `15DCB957634A6F1E47110AD9A7CA564683560798DD472DF8D399FF7E0368A142` |
| `bundle://proof/SB009/transcripts/changed-file-hashes.txt` | `65520769F035E2D45F58F86DE73F895C24F87A6164B60F1EE3DD64675B27A910` |
| `bundle://proof/SB009/transcripts/prepared-validator-after-sb009.txt` | `241DD411D4272C91EB3810FD05FE965B3E707F778670FDE97F4D21CE734736CC` |

## Semantic Evidence

- Semantic invariant contract: `bundle://proof/SB009/semantic-invariants.md`
- Source assertions: `repo://src/CanDoItAll.Web/Program.cs` registers infrastructure, runtime database switching, runtime modules, API services, project-structure API routes, process API routes, Razor components, health checks, and runtime database bootstrap.
- Source assertions: `repo://src/CanDoItAll.Composition/ModuleAssemblies.cs` includes `ProcessesModuleAssemblyMarker`, and `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` calls `AddProcessesModule(configuration)`.
- Source assertions: `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` registers `ProcessesService`, `ProcessTemplateCatalogService`, and `IProcessRunAutomationDispatchService`.
- Runtime proof: `repo://tests/CanDoItAll.Tests.Integration/ApplicationStartupIntegrationTests.cs` starts a `WebApplication` on an ephemeral port, uses provider validation, maps API and Razor endpoints, bootstraps the test database, gets HTTP 200 from `/health`, reads process templates from `/api/processes/templates`, and resolves process runtime services from DI.

## Shallow-Pass Trap

The smoke proof is not accepted if it only builds a service provider or returns a non-empty test output. `bundle://proof/SB009/transcripts/semantic-positive-source-audit.txt` requires live HTTP health, process template API visibility, `ProcessesService` resolution, and `IProcessRunAutomationDispatchService` resolution.

## Adversarial Negative Proof

`bundle://proof/SB009/transcripts/forbidden-drift-scan.txt` proves the changed startup/composition surface does not introduce transient bundle path coupling or generic driver runtime-host registration. `bundle://proof/SB009/transcripts/anti-stub-audit-startup-test.txt` proves the new smoke test has no placeholder assertion, skipped test, TODO, or stubbed startup behavior.

## Production Behavior Artifact Matrix

| Signal | Source artifact | Runtime proof |
| --- | --- | --- |
| Web host starts | `repo://tests/CanDoItAll.Tests.Integration/ApplicationStartupIntegrationTests.cs` | `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt` |
| Health endpoint is mapped and ready | `repo://src/CanDoItAll.Web/Program.cs` and smoke host mapping | `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt` |
| Process templates are visible through HTTP API | `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs` and smoke test assertions | `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt` |
| Process runtime services resolve from DI | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt` |

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| Prove web application starts on the current branch with current composition/DI. | Solved | `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt` and `bundle://proof/SB009/transcripts/web-project-build.txt`. |
| Runtime host/registry/selector/DI hook must remain blocked unless explicitly approved. | Preserved | `bundle://proof/SB009/transcripts/forbidden-drift-scan.txt`. |
