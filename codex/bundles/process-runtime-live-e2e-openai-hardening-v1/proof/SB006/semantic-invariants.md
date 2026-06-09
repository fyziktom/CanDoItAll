# SB006 Semantic Invariants

## Status
Completed.

## Invariant SB006_INV_001
The web app startup composition must build, expose readiness at `/health`, map process APIs, expose process templates, and resolve process runtime services without introducing UI/media drift or a generic driver runtime host.

## Shallow-Pass Trap
A shallow implementation could pass a build while omitting `/health`, not mapping `/api/processes`, or failing to register process dispatch services. SB006 rejects that by combining a web build, startup integration tests that call `/health` and `/api/processes/templates`, direct service-resolution assertions, hosted-worker policy tests, and source assertions over the real startup files.

## Failing-First And Negative Proof
- Adversarial negative proof: `bundle://proof/SB006/transcripts/red-team-startup-wiring-rejection.txt` rejects a fake startup snippet missing process module, health, and process API wiring.
- Source assertion proof: `bundle://proof/SB006/transcripts/startup-critical-source-assertions.txt` proves those required tokens exist in the real startup source.

## Positive Proof
- Web build passed with 0 warnings and 0 errors: `bundle://proof/SB006/transcripts/web-build-no-restore.txt`
- Startup critical integration tests passed: `bundle://proof/SB006/transcripts/startup-critical-integration-tests.txt`
- No transient bundle-path scan passed: `bundle://proof/SB006/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan passed: `bundle://proof/SB006/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No UI/media drift scan passed: `bundle://proof/SB006/transcripts/no-ui-media-drift-scan.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| Runtime module composition | `AddCanDoItAllRuntimeModules` | Web startup | Registers process module and dependent runtime modules before app build completes | `bundle://proof/SB006/transcripts/startup-critical-source-assertions.txt` |
| Health readiness route | `app.MapHealthChecks("/health")` | Startup smoke and operators | Returns readiness state after bootstrap marks runtime ready | `bundle://proof/SB006/transcripts/startup-critical-integration-tests.txt` |
| Process templates API | `MapCanDoItAllApi` and `MapProcessesApi` | UI/API launch surfaces | Exposes `/api/processes/templates` and template detail routes for downstream launch proof | `bundle://proof/SB006/transcripts/startup-critical-integration-tests.txt` |
| Process runtime services | `AddProcessesModule` | Runtime, dispatch, UI, API, scheduler/workflow downstream gates | Registers `ProcessesService`, template catalog, dispatch service, runtime tool provider, and hosted-worker policy | `bundle://proof/SB005/process-module-registration-proof.md` |

## Runtime-Host Boundary
SB006 does not add a process-driver runtime host, registry, selector, DI auto-registration, manager command, scheduler hook, workflow hook, process-state mutation through read-only drivers, or Process Core runtime orchestration. The anti-stub/runtime-host drift scan is the guard for this boundary.
