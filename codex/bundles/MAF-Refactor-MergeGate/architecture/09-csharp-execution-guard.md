# C# execution architecture guard

## Current-state inventory

Baseline CodeAnalytics snapshot `snap-20260808131134-d5be9d01` loaded 12 affected projects and
1,401 documents. The snapshot has no blocking load error. Its 78 diagnostics are informational DI
limitations around factory-based registrations; negative DI evidence must therefore be confirmed from
exact source and tests.

Exact owners inspected through CodeAnalytics:

| Responsibility | Current owner | Evidence and risk |
|---|---|---|
| Persisted turn authority parsing | `AgentTurnContextMetadata` in Core | `AgentTurnContextMetadata.cs`; parser conflates absent and malformed |
| Runtime authority restoration | `AgentFrameworkWorkspaceExecutionService` partial cluster in Core | High-fan-in execution owner; change only the restoration slice |
| Source authority resolution | `CanonicalAgentExecutionAuthorityResolver` in Modules.AgentFramework | 15 members; currently owns product source semantics |
| Tool-policy composition | `AgentToolInvocationPolicyPipeline` in Core | Related `AgentToolInvocationPolicy.cs` is 3,675 lines; do not expand it |
| Provider-framework policy mapping | `MafRuntimeAgentFactory` in MAF | Must remain process-semantic-free |
| Durable lease persistence/cleanup | Core execution partial plus `WorkspaceExecutionRunProcessLeaseStore` | Lease store source is 888 lines; keep cleanup extraction cohesive |
| File conversation persistence | `FileLlmConversationStore` | 14 members; instance-local concurrency is not a shared-store boundary |
| Ordinary turn transaction | `LlmConversationService` plus contracts | Canonical application transcript and active-turn compensation |
| Lightweight invocation | `ProviderBackedLlmInvocationAdapter` | Provider adapter owns bounded retry accounting |
| Workflow usage projection | `WorkflowLlmComponentInvoker` | Consumer of typed invocation failures |

Current tests exist in the six SB00 primary test files plus
`CanonicalAgentExecutionAuthorityResolverTests.cs`. Missing negative cases are owned explicitly by
SB00 before production changes.

## Boundary map

- Core owns provider-neutral authority contracts, restoration validation, policy composition, and
  trusted execution-scope coordination.
- Source-publishing modules own their source-authority provider implementations and registrations.
- MAF maps framework calls and consumes effective policy results without product/process semantics.
- LLM Abstractions owns strongly typed conversation/invocation contracts only.
- LLM Conversations owns transaction orchestration and persistence implementations.
- LLM ProviderRuntime owns provider-attempt retry and aggregation.
- Workflows Runtime owns workflow observation projection.
- Modules.AgentFramework remains the composition root and must not regain product-source logic.

No new project is planned. No new broad interface, service locator, partial file, or product reference
is authorized. New top-level collaborators are allowed only where SB04 or SB05 needs a real lifecycle or
test seam and the dependency direction below remains intact.

## Dependency direction

The baseline snapshot reports 38 project-reference edges and no project-reference cycle. Required
direction remains:

```text
Modules / composition -> Runtime / Core -> Models
LLM Conversations / ProviderRuntime -> LLM Abstractions -> Models
Workflows Runtime -> Core + LLM Abstractions / ProviderRuntime -> Models
MAF -> Core + Models
Tests -> affected product projects
```

Forbidden edges:

- MAF -> any `Modules.*` project;
- LLM Abstractions -> Core, MAF, workspace, process, workflow, module, or persistence implementations;
- Core -> source-publishing module implementations;
- circular module ownership introduced to move an authority provider.

CodeAnalytics reports existing module/type relationship cycles, but no project cycle. SB02 and every
project-reference change must rerun project dependency proof and reject any new project cycle.

## Pattern selection records

| Work | Force | Selected pattern | Rejected simpler option | Test seam |
|---|---|---|---|---|
| SB01 | Tri-state trust result | Strongly typed result/state value | Nullable projection, because it cannot distinguish malformed from absent | Parser and restoration tests without provider construction |
| SB02 | Multiple module-owned source implementations | DI registry/provider strategy | Hard-coded factory list, because it centralizes product semantics | Resolver tests with explicit provider sets plus module registration tests |
| SB03 | Ordered context enrichment plus decision | Pipeline result containing effective context and decision | Returning only the decision, because downstream policy loses evidence | Pipeline/MAF tests with unrelated cloning and real process contributor |
| SB04 | Scope-specific cleanup service construction | Narrow scope-aware factory/coordinator | Fixed organization cleaner or service locator | Cleanup tests with distinct organization/project roots |
| SB05 | Shared file-resource serialization | Canonical-path keyed coordinator | Instance lock, because scoped instances do not share it | Independent store instances racing one document |
| SB06 | Recoverable multi-step turn | Explicit durable compensation state | Reconstructing rollback from post-admission state | Failure/cancel/abandon/recovery tests |
| SB07 | Bounded attempt accounting | Immutable checked aggregation | Final-attempt-only counters | Adapter tests with attempt-specific usage |
| SB08 | Unsafe unused activation | Composition removal with architecture guard | Profile fallback, because it would hide stale scope | Isolated library DI plus no-production-consumer guard |

## Testability and partial-class policy

- Every behavior-changing SB starts from a deterministic failing characterization test in SB00.
- Extracted coordinators/providers must be instantiated directly in unit tests; DI-resolution-only proof
  is insufficient.
- Composition changes require a module/runtime smoke in addition to isolated unit tests.
- No new production partial file is allowed. Existing execution-service partials may receive a minimal
  delegation/edit only when the responsibility remains orchestration and no duplicate behavior remains.
- Shallow wrappers, hard-coded fixtures, `IServiceProvider` lookup in core behavior, and silent fallback
  implementations fail the architecture gate.

## Composition and checkpoint impact

SB02 changes provider registration ownership; SB03 changes the policy result consumed by MAF; SB04
changes scope-specific cleanup construction; SB08 removes premature production registration. Each must
prove both isolated behavior and the existing composition root. `plan/architecture-checkpoints.md`
defines the unlock and reopen decisions.
