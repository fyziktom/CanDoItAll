# v1 Contract Migration and Alpha Verifier Compatibility

## Status
- Subbundle: `SB041`
- Current driver contract version: `1.10.0`
- Compatibility line: `v1.x verification-only alpha`
- Runtime host approval: not granted in this bundle.

## Migration Rules
- Major compatibility rule: v1 consumers must reject any contract with `Major != 1`.
- Minor version additions are additive only when they preserve read-only verification semantics, keep existing enum ordinals stable, and include compatibility tests plus migration notes.
- Patch version changes may clarify behavior or fix bugs without changing public contract shape.
- Core descriptor family ordinals are compatibility-significant and must remain stable: `ExecutionEvidence = 1`, `FinalizerEvidence = 2`, `RetryDiagnostics = 3`, `ArtifactProjectionEvidence = 4`, and `ArtifactProjectionValidation = 5`.
- Gateway lane descriptors may reference only the allow-listed primary descriptor families: transcript/runtime use `ExecutionEvidence`, artifact evidence uses `ArtifactProjectionEvidence`, and Office/business lanes use non-Core evidence references with no Core descriptor family.
- Supplied evidence content remains caller-provided and in-memory. Drivers must not resolve arbitrary files, issue HTTP requests, call connectors, read storage, or fetch workspace content.
- `ExecutionCapableFuture` remains a denied future marker. It is not an approval for shell execution, process mutation, workspace writes, storage writes, finalizer application, retry scheduling, or manager-command dispatch.
- New v1.x diagnostic categories or descriptor families require an explicit `ProcessDriverContractVersion.Current` review, snapshot update, focused compatibility tests, and this document update before downstream packages consume them.

## Version History
| Source | Contract version | Compatibility note |
| --- | --- | --- |
| SB025 audit facts | `1.5.0` | Audit facts gained explicit typed lane and evidence-reference fields. |
| SB028 Office evidence | `1.6.0` | Office evidence supplied-content envelope and `OfficeEvidenceRead` scope were added without Graph, connector, DI, or external-call behavior. |
| SB031 business analysis | `1.7.0` | Business analysis supplied-content envelope and `BusinessAnalysisRead` scope were added without CRM/business-record mutation behavior. |
| SB032 business diagnostics | `1.8.0` | Business diagnostic categories were added for supplied text markers only. |
| SB034 artifact evidence scope | `1.9.0` | `ArtifactEvidenceRead` scope was added while reusing `CoreDescriptorPayload` for supplied artifact descriptors. |
| SB035 artifact diagnostics | `1.10.0` | Artifact lineage, trust/sensitivity, and satisfaction diagnostic categories were added for supplied Core snapshots only. |
| SB040 compatibility guard | `1.10.0` | Descriptor family ordinals, gateway family mappings, and version-history docs are locked by tests. |

## Alpha Verifier Behavior Matrix
| Package or component | Scope | Supplied evidence | Primary descriptor family | Read-only behavior | Denied behavior |
| --- | --- | --- | --- | --- | --- |
| `CanDoItAll.Processes.Drivers.TranscriptVerification` | `DotNetRustTranscriptVerification` | `TranscriptText` bound to `CommandTranscript` | `ExecutionEvidence` | Classifies supplied .NET/Rust transcript text and returns diagnostics/audit facts only. | Denies command execution, restore, workspace writes, process mutation, external calls, and storage writes. |
| `CanDoItAll.Processes.Drivers.RuntimeEvidence` | `RuntimeFactsRead` | `CoreDescriptorPayload` bound to `CoreDescriptor` | `ExecutionEvidence` | Reads supplied execution/finalizer/retry/projection descriptor snapshots and returns contradiction diagnostics only. | Denies process mutation, finalizer application, retry scheduling, provider repair, workspace writes, storage writes, and external calls. |
| `CanDoItAll.Processes.Drivers.OfficeEvidence` | `OfficeEvidenceRead` | `OfficeEvidencePayload` bound to `OfficeReadonlyArtifact` | none | Reads supplied email/document metadata and text only. | Denies Graph calls, connector calls, email category mutation, task creation, attachment fetches as external Office calls, document writes, workspace writes, and storage writes. |
| `CanDoItAll.Processes.Drivers.BusinessAnalysis` | `BusinessAnalysisRead` | `BusinessAnalysisPayload` bound to `BusinessReadonlyArtifact` | none | Reads supplied deliverable/evidence text and marker metadata only. | Denies CRM/business-record mutation, task creation, process transition, workspace writes, storage writes, and external calls. |
| `CanDoItAll.Processes.Drivers.ArtifactEvidence` | `ArtifactEvidenceRead` | `CoreDescriptorPayload` bound to `CoreDescriptor` | `ArtifactProjectionEvidence` and `ArtifactProjectionValidation` | Reads supplied artifact projection, validation, expectation, and record snapshots only. | Denies artifact writes, file reads, storage writes, workspace writes, runtime host behavior, registry/selector/provider use, and external calls. |
| `CanDoItAll.Processes.Drivers.ObservationAggregation` | typed audit lanes from existing responses | `ProcessDriverVerificationResponse` envelopes only | existing response evidence only | Aggregates already-produced verifier responses into read-only snapshot envelopes. | Does not invoke verifiers, discover drivers, register DI services, persist observations, schedule work, trigger commands, or mutate state. |

## Consumer Migration Checklist
- Send `ProcessDriverVerificationRequest.ContractVersion` with `Major == 1`; reject any other major version before evidence analysis.
- Use the lane-specific `ProcessDriverCapabilityScopeKind`, `ProcessDriverPermissionMode`, and read-only operation set. Do not rely on strings for lane or command identity.
- Bind supplied content to an included evidence reference with an approved URI, bounded size, valid SHA-256 hash, expected content kind, expected content type, and matching content hash.
- Treat diagnostics as bounded summaries. Do not parse raw supplied payload text from diagnostics or audit facts.
- Treat every alpha verifier response as read-only evidence. A response can support manual decision-making, but it must not be treated as permission to mutate process state or external systems.
- Keep Office, business, artifact, and observation aggregation outputs out of persistence, scheduling, manager-command, workflow, and runtime-host paths until a future explicitly approved bundle defines those responsibilities.

## Runtime Non-Goals
- No generic driver discovery.
- No runtime host, registry, selector, provider, DI registration, manager command, scheduler, workflow hook, or process lifecycle integration.
- No shell execution, package restore, Office/Graph runtime call, CRM call, arbitrary file read, workspace write, storage write, process mutation, claim mutation, transition mutation, finalizer application, provider repair, or retry scheduling.
- No UI or browser behavior is part of the v1 verification-only contract line.
- Runtime-host approval gates and non-goals are tracked in `architecture/10-runtime-host-approval-matrix.md`; that matrix keeps registry, selector, DI, manager, scheduler, workflow, and execution-capable driver surfaces `Not approved`.

## Reopen Triggers
- Reopen SB041 if `ProcessDriverContractVersion.Current` changes without updating this document, the API snapshot, and compatibility tests.
- Reopen SB041 if Core descriptor family ordinals change or new families are consumed without migration notes and focused compatibility tests.
- Reopen SB041 if any alpha verifier starts reading from runtime systems, storage, workspace files, connectors, HTTP, manager commands, or mutable process state.
- Reopen SB041 if documentation implies that runtime host, DI registration, scheduler, workflow, command execution, Office/Graph runtime calls, CRM mutation, workspace writes, or storage writes are approved in this bundle.
