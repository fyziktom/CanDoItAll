# Process Runtime Host Code-First Integration Gate

## Purpose

This document records the runtime-host integration state shipped by the current
code-first integration gate.

It exists for maintainers who need to understand what is now implemented in
source, what remains deliberately blocked, and which tests protect the boundary.

The gate is intentionally read-only. It improves runtime-host observability,
manager readback, audit query lifecycle, dry-run planning, and static capability
descriptors without adding execution-capable process drivers.

## Non-Goals

- Do not introduce an effectful generic process driver host.
- Do not add driver self-registration.
- Do not add reflection-based driver discovery.
- Do not add a fallback selector.
- Do not mutate process state through drivers.
- Do not mutate transition state through drivers.
- Do not apply finalizers through drivers.
- Do not schedule retries through drivers.
- Do not write workspace files through drivers.
- Do not write managed storage through drivers.
- Do not execute shell commands through drivers.
- Do not restore packages through drivers.
- Do not call Office, Graph, CRM, provider repair, or HTTP APIs through drivers.
- Do not put domain-specific driver terms into Process Core.

## Implemented Source Surfaces

| Area | File | Role |
| --- | --- | --- |
| Contract boundary | `src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeHostContractModels.cs` | Public runtime-host contract snapshot, version, surface, and read-only safety validation. |
| Driver operation lists | `src/CanDoItAll.Processes.Drivers.Abstractions/Permissions/ProcessDriverOperationRules.cs` | Explicit read-only and side-effect operation lists used by static descriptors and tests. |
| Static module catalog | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationHostCapabilityCatalog.cs` | Internal verification-host capability descriptors without discovery or registration. |
| Verification host | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs` | Uses exact lane selection and returns capability-keyed success or denial. |
| Verification models | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostModels.cs` | Carries capability key, contract snapshot, denial code, audit record, and mutation flags. |
| Runtime status | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostStatus.cs` | Exposes readiness, audit-store classification, static capability status, and contract snapshot. |
| Manager facade | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs` | Projects capability key, evidence count, audit hash, denial metadata, and contract into readback DTOs. |
| Audit store | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs` | Adds bounded retention-candidate query without deleting or mutating audit records. |
| Scheduler job runner | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs` | Returns scheduler/workflow read-only job contract metadata. |
| Dry-run host | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs` | Produces capability-keyed dry-run plans and denied operation/surface lists without effects. |

## Contract Snapshot

Every runtime-host-facing response now has a `ProcessRuntimeHostContractSnapshot`
or an equivalent manager readback projection.

The snapshot fields are:

- `Version`
- `Surface`
- `DryRunOnly`
- `NoMutationPerformed`
- `AllowsProcessMutation`
- `AllowsTransitionMutation`
- `AllowsFinalizerMutation`

The current version is:

- `ProcessRuntimeHostContractVersion.Current`
- `1.1.0`

The current surfaces are:

- `VerificationHost`
- `DryRunExecution`
- `ManagerReadback`
- `OperatorStatus`
- `SchedulerWorkflowReadOnlyJob`

The safety rule is simple and explicit:

- `DryRunOnly` must be `true`.
- `NoMutationPerformed` must be `true`.
- `AllowsProcessMutation` must be `false`.
- `AllowsTransitionMutation` must be `false`.
- `AllowsFinalizerMutation` must be `false`.

`ProcessRuntimeHostContractSnapshot.ValidateReadOnlySafety()` returns typed
violations instead of silently accepting unsafe snapshots.

The violation kinds are:

- `ProductionExecutionAllowed`
- `MutationNotRecordedAsDenied`
- `ProcessMutationAllowed`
- `TransitionMutationAllowed`
- `FinalizerMutationAllowed`

## Capability Catalog

The process module owns `ProcessVerificationHostCapabilityCatalog`.

The catalog is internal to the module. It does not add public runtime capability
to `CanDoItAll.Processes.Drivers.Abstractions`.

The catalog intentionally uses explicit static descriptors.

It does not use:

- `Assembly.GetTypes`
- `AppDomain.CurrentDomain.GetAssemblies`
- `Activator.CreateInstance`
- DI service scanning
- driver self-registration
- fallback selection

Every descriptor includes:

- `Key`
- `Kind`
- `ContractSurface`
- `PermissionMode`
- `AllowedOperations`
- `DeniedOperations`
- `ReflectionDiscoveryAllowed`
- `SelfRegistrationAllowed`
- `ExecutionAllowed`
- `IsStaticReadOnlyDescriptor`

The verification-lane key shape is:

- `verification:{ProcessDriverVerificationGatewayLane}`

The dry-run gate key is:

- `dry-run:execution-capable-future-gate`

## Verification Capability Matrix

| Key | Contract surface | Permission mode | Execution allowed | Discovery allowed | Self-registration allowed |
| --- | --- | --- | --- | --- | --- |
| `verification:DotNetRustTranscriptVerification` | `VerificationHost` | `VerificationOnly` | `false` | `false` | `false` |
| `verification:RuntimeEvidenceConsistency` | `VerificationHost` | `ManagerReadonly` | `false` | `false` | `false` |
| `verification:ArtifactEvidenceConsistency` | `VerificationHost` | `VerificationOnly` | `false` | `false` | `false` |
| `verification:OfficeEvidenceRead` | `VerificationHost` | `VerificationOnly` | `false` | `false` | `false` |
| `verification:BusinessAnalysisRead` | `VerificationHost` | `VerificationOnly` | `false` | `false` | `false` |
| `dry-run:execution-capable-future-gate` | `DryRunExecution` | `ExecutionCapableFuture` | `false` | `false` | `false` |

## Allowed Read-Only Operations

`ProcessDriverOperationRules.ReadonlyVerificationOperations` is the canonical
read-only operation list.

Allowed read-only operations are:

- `InspectExistingEvidence`
- `ReturnDiagnostics`
- `ReadProcessFacts`
- `ExplainDenial`

These operations may appear in verification descriptors.

They must not cause process mutation.

They must not cause workspace mutation.

They must not cause storage mutation.

They must not call external systems.

## Denied Side-Effect Operations

`ProcessDriverOperationRules.SideEffectOperations` is the canonical side-effect
operation list.

Denied operations are:

- `MutateProcessState`
- `ExecuteCommand`
- `RestorePackage`
- `WriteArtifact`
- `WriteWorkspaceStorage`
- `CallOfficeGraph`
- `MutateEmailCategory`
- `CreateTask`
- `MutateBusinessRecord`
- `ApplyTransition`
- `ClaimDispatch`
- `ApplyFinalizer`
- `ScheduleRetry`

Descriptors must include these operations in `DeniedOperations`.

Descriptors must never include these operations in `AllowedOperations`.

## Operator Status

`ProcessVerificationRuntimeHostStatusDto` is the operator-facing status surface.

It includes:

- `CorrelationId`
- `RequestedBy`
- `RequestedAt`
- `Enabled`
- `EmergencyDisabled`
- `Readiness`
- `AuditStoreKind`
- `UsesDurableAuditStore`
- `Lanes`
- `Capabilities`
- `NoMutationPerformed`
- `AllowsProcessMutation`
- `AllowsTransitionMutation`
- `AllowsFinalizerMutation`
- `Contract`
- `SupportsAuditRetentionQuery`

The status service classifies audit storage as:

- `DurableEfCore`
- `TestInMemory`
- `Unknown`

The status service reports readiness as:

- `Ready`
- `EmergencyDisabled`
- `MissingLaneRegistration`
- `AuditStoreNotClassified`

The status response intentionally exposes capability summaries so operators can
see that verification lanes and the dry-run gate are static, read-only, and
non-execution descriptors.

## Manager Readback

`ProcessManagerReadOnlyVerificationReadbackDto` is the manager-facing readback
shape.

It includes:

- `Status`
- `CapabilityKey`
- `Lane`
- `ProcessRunId`
- `StepRunId`
- `CallerContext`
- `ProjectionMode`
- `ProjectionSource`
- `ProjectionAttached`
- `AuditRecordId`
- `ResponseCount`
- `DiagnosticCount`
- `Diagnostics`
- `AuditRecords`
- `EvidenceReferenceCount`
- `AuditRecordObservationHash`
- `DenialCategory`
- `DenialCode`
- `DenialMessage`
- `NoMutationPerformed`
- `AllowsProcessMutation`
- `AllowsTransitionMutation`
- `AllowsFinalizerMutation`
- `RequestedAt`
- `ObservedAt`
- `Contract`

The readback surface now gives callers enough information to correlate a
verification readback row with the static capability catalog.

The readback surface also gives callers enough information to reconcile the
visible audit hash with the audit record collection returned for the same run.

## Denial Shape

Verification host denials include:

- `CapabilityKey`
- `Category`
- `Code`
- `Message`
- `Lane`
- `ProcessRunId`
- `StepRunId`
- `RequestedBy`
- `RequestedAt`
- `AuditRecord`
- `NoMutationPerformed`
- `AllowsProcessMutation`
- `AllowsTransitionMutation`
- `AllowsFinalizerMutation`
- `Contract`

Denial categories are:

- `OperationalPolicy`
- `LaneConfiguration`
- `RequestValidation`
- `ResourceLimit`
- `VerificationOutcome`

Denial codes are:

- `HostDisabled`
- `LaneDisabled`
- `UnsupportedLane`
- `MissingLaneRegistration`
- `MissingLanePayload`
- `PayloadLimitExceeded`
- `SuppliedEvidenceContentLimitExceeded`
- `NoResponsesProduced`

Denials still append audit records.

Denials still report no mutation.

Denials still carry a verification-host contract snapshot.

## Durable Audit

The audit store supports:

- append-only records,
- bounded list queries,
- scoped query filters,
- recorded-at lower bound,
- recorded-before upper bound,
- retention-candidate listing,
- in-memory test store behavior,
- EF Core production store behavior.

The retention query is read-only.

It does not delete records.

It does not mutate records.

It returns oldest records first.

It validates limit bounds.

It caps limits at `500`.

The retention query type is:

- `ProcessVerificationAuditRetentionQuery`

The query service method is:

- `ListRetentionCandidatesAsync`

## Scheduler And Workflow Read-Only Job

`ProcessReadOnlyVerificationJobRunner` remains a read-only orchestration layer.

It routes through:

- `IProcessManagerReadOnlyVerificationFacade`

It does not instantiate drivers directly.

It does not call driver packages from scheduler/workflow services.

It returns:

- `ProcessReadOnlyVerificationJobRunResult`

The result includes:

- manager readback,
- audit records,
- no-mutation flags,
- `SchedulerWorkflowReadOnlyJob` contract surface.

## Dry-Run Host

`ProcessDryRunExecutionHost` evaluates requested future execution surfaces
through `ProcessExecutionCapableDriverFutureGate`.

It returns:

- `CapabilityKey`
- `RequestId`
- `ProcessRunId`
- `StepRunId`
- `RequestedBy`
- `RequestedAt`
- `Decision`
- `GateResult`
- `Plan`
- `DeniedSurfaces`
- `DeniedOperations`
- `AuthorizationGaps`
- `NoMutationPerformed`
- `AllowsProcessMutation`
- `AllowsTransitionMutation`
- `AllowsFinalizerMutation`
- `Contract`

The dry-run host does not execute effects.

The dry-run host records denied surfaces.

The dry-run host records denied operations.

The dry-run host records missing authorization evidence.

The dry-run host records emergency-stop evidence gaps.

The dry-run host uses the capability key:

- `dry-run:execution-capable-future-gate`

## Future Gate Requirements

Execution-capable drivers remain blocked unless all future requirements are
satisfied.

Current future-gate requirements are:

- `SourceBackedApprovalBundle`
- `LifecycleOwnership`
- `CancellationTimeoutFailureHandoff`
- `ImmutableAuditPersistence`
- `SandboxBoundary`
- `AuthorizationApprovalRevocation`
- `PublicApiCompatibility`
- `MaliciousCorpus`
- `RedTeamProof`

Authorization evidence requires:

- approval grant present,
- revocation check passed,
- emergency stop clear.

Missing authorization gaps are:

- `ApprovalGrantMissing`
- `RevocationCheckMissing`
- `EmergencyStopActiveOrUnknown`

## Process Core Boundary

Process Core remains generic.

Process Core must not reference:

- process driver implementations,
- process module runtime hosts,
- infrastructure,
- EF Core,
- UI,
- MAF,
- OpenAI,
- workspace services,
- storage services.

Runtime host contract types live in `CanDoItAll.Processes.Contracts`, not
Process Core.

Module runtime-host capability descriptors live in `CanDoItAll.Modules.Processes`,
not Process Core.

Driver abstraction lane descriptors remain verification-only contract data.

## Driver Abstraction Boundary

`CanDoItAll.Processes.Drivers.Abstractions` remains public-runtime-free.

It may define:

- verification requests,
- verification responses,
- diagnostics,
- audit facts,
- supplied evidence references,
- lane descriptors,
- permission modes,
- operation vocabularies,
- redaction descriptors.

It must not define:

- runtime host interfaces,
- runtime providers,
- runtime selectors,
- runtime registries,
- DI registration helpers,
- manager commands,
- scheduler hooks,
- workflow hooks,
- hosted services.

The public type count and surface hash remain guarded by unit tests.

## Source Tests

Focused integration tests now cover:

- runtime host status readiness,
- static capability status exposure,
- durable audit store classification,
- in-memory audit store classification,
- audit retention query behavior,
- manager readback success metadata,
- manager readback denial metadata,
- manager readback JSON serialization,
- large-screen API readback smoke shape,
- run-detail denial readback smoke shape,
- scheduler/workflow read-only job routing,
- dry-run denial planning,
- dry-run authorization and emergency-stop gaps,
- static verification-host capability catalog,
- driver-reference allow-listing,
- code-first source inventory.

Focused unit tests now cover:

- public contract snapshot safety validation,
- driver abstraction public surface count,
- driver abstraction runtime-free guard,
- verification gateway static lane descriptors,
- Process Core driver-free guard,
- module contract placement outside Process Core.

## Validation Commands

Build command:

```powershell
dotnet build CanDoItAll.slnx --configuration Debug
```

Focused integration command:

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-build --filter "FullyQualifiedName~ProcessDomainEvidenceReadOnlyAdapterTests|FullyQualifiedName~ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests|FullyQualifiedName~ProcessRuntimeHostCodeFirstGuardTests"
```

Focused unit command:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Debug --no-build --filter "FullyQualifiedName~ProcessDriverContractApiVerificationBoundaryTests"
```

Full unit command:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Debug --no-build
```

Live OpenAI smoke guard command:

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-build --filter "FullyQualifiedName~LiveProcessRunOpenAiSmokeIntegrationTests"
```

## Live Smoke Policy

The live OpenAI process-run smoke remains opt-in.

It requires:

- `CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION`
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`
- `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL`
- `CANDOITALL_LIVE_PROCESS_RUN_TIMEOUT_SECONDS`
- `CANDOITALL_LIVE_PROCESS_RUN_MAX_TOTAL_TOKENS`
- `OPENAI_API_KEY`

When the opt-in variables are absent, the live process-run path is skipped by
the test body, and the guard tests still validate explicit model, timeout, and
token-budget requirements.

Secrets must never be printed.

The API key value must never be logged.

Exception messages must not include `OPENAI_API_KEY` values or `sk-` tokens.

## Source Scan Expectations

The Core dependency scan should produce no matches for:

- `CanDoItAll.Processes.Drivers`
- `CanDoItAll.Modules`
- `CanDoItAll.Infrastructure`
- `Microsoft.EntityFrameworkCore`
- `OpenAI`
- `Npgsql`
- workspace service dependencies,
- storage service dependencies.

The reflection discovery scan should produce no matches for:

- `Assembly.GetTypes`
- `GetAssemblies`
- `GetTypes`
- `Activator.CreateInstance`
- reflection-based driver discovery.

The fallback/self-registration scan should produce no matches for:

- `FallbackSelector`
- fallback driver selectors,
- driver self-registration,
- driver registries,
- driver service collection registration.

The bundle-coupling scan should produce no matches for this bundle id under
`src`, `tests`, or `docs`.

The secret scan should produce no `sk-` key-like tokens.

## Reopen Triggers

Reopen this gate if any runtime-host response loses `NoMutationPerformed`.

Reopen this gate if any runtime-host response allows process mutation.

Reopen this gate if any runtime-host response allows transition mutation.

Reopen this gate if any runtime-host response allows finalizer mutation.

Reopen this gate if verification lane descriptors gain side-effect operations.

Reopen this gate if static capability descriptors allow execution.

Reopen this gate if static capability descriptors allow reflection discovery.

Reopen this gate if static capability descriptors allow self-registration.

Reopen this gate if Process Core references driver packages.

Reopen this gate if driver abstractions gain runtime host/provider/selector types.

Reopen this gate if manager readback loses audit id/hash correlation.

Reopen this gate if audit retention starts deleting or mutating records.

Reopen this gate if scheduler/workflow services call driver packages directly.

Reopen this gate if live smoke validation logs secrets.

Reopen this gate if the code-first ratio falls below the bundle-required `2.0`.

## Maintenance Checklist

Use this checklist when changing the runtime-host surface.

- Confirm the changed surface still has a contract snapshot.
- Confirm the changed surface still reports `DryRunOnly`.
- Confirm the changed surface still reports `NoMutationPerformed`.
- Confirm the changed surface still denies process mutation.
- Confirm the changed surface still denies transition mutation.
- Confirm the changed surface still denies finalizer mutation.
- Confirm status still returns all verification lane capabilities.
- Confirm status still returns the dry-run gate capability.
- Confirm status still classifies the audit store.
- Confirm status still reports retention-query support.
- Confirm readback still returns `CapabilityKey`.
- Confirm readback still returns `AuditRecordId`.
- Confirm readback still returns `AuditRecordObservationHash`.
- Confirm readback still returns `EvidenceReferenceCount`.
- Confirm readback still returns denial category.
- Confirm readback still returns denial code.
- Confirm readback still returns denial message.
- Confirm readback JSON keeps web serializer compatibility.
- Confirm dry-run results still return `CapabilityKey`.
- Confirm dry-run results still include denied surfaces.
- Confirm dry-run results still include denied operations.
- Confirm dry-run results still include authorization gaps.
- Confirm dry-run plans still include a no-mutation step.
- Confirm the static catalog does not use reflection.
- Confirm the static catalog does not use DI discovery.
- Confirm the static catalog does not permit self-registration.
- Confirm the static catalog does not permit execution.
- Confirm allowed operations are read-only.
- Confirm denied operations include every side-effect operation.
- Confirm driver abstractions do not gain public runtime host types.
- Confirm Process Core does not reference driver implementations.
- Confirm Process Core does not reference module runtime hosts.
- Confirm Process Core does not reference EF Core.
- Confirm Process Core does not reference infrastructure.
- Confirm Process Core does not reference workspace services.
- Confirm Process Core does not reference storage services.
- Confirm scheduler code does not instantiate drivers.
- Confirm workflow code does not instantiate drivers.
- Confirm manager facade routes through the runtime host.
- Confirm audit retention queries do not delete records.
- Confirm audit retention queries do not mutate records.
- Confirm audit retention query limits remain bounded.
- Confirm audit query time windows reject inverted ranges.
- Confirm live smoke remains opt-in.
- Confirm live smoke requires explicit model.
- Confirm live smoke requires explicit timeout.
- Confirm live smoke requires explicit token budget.
- Confirm live smoke does not log API key values.
- Confirm source scans include reflection-discovery tokens.
- Confirm source scans include fallback-selector tokens.
- Confirm source scans include bundle-coupling tokens.
- Confirm source scans include key-like secret tokens.
- Confirm focused integration tests cover status.
- Confirm focused integration tests cover manager readback.
- Confirm focused integration tests cover audit retention.
- Confirm focused integration tests cover dry-run.
- Confirm focused integration tests cover scheduler/workflow read-only jobs.
- Confirm focused unit tests cover Process Core genericity.
- Confirm focused unit tests cover driver abstraction runtime-free public surface.
- Confirm focused unit tests cover contract snapshot safety.
- Confirm full unit tests pass before release.
