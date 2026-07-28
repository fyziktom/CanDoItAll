# SB05 C# Architecture Review

## C# Architecture Gate Result

Status: Pass with follow-up

Reviewed snapshot: `snap-20260727233256-654bc9d9`

The snapshot is a full `CanDoItAll.slnx` capture with 12 projects and 963 source
documents. CodeAnalytics recorded 1,182 findings and 216 diagnostics; those raw
counts are not treated as 1,182 SB05 regressions.

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| P2 | Database profile switching invokes subscribers synchronously, so a blocked subscriber can delay the control-plane switch | `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`; `bundle://proof/SB05/concurrency-invariants.md` | Introduce a bounded notification policy in a separate hardening change |
| P2 | File WAL recovery proves process interruption, not physical-media flush durability | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs`; generic 6/6 and combined 33/33 | Define and test the required power-loss durability contract before claiming it |
| P2 | A remote provider commit can occur after the final scalar revision probe and before provider use | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` | Add a stronger cross-host lease/version contract only if real multi-host consistency requires it |

No P0/P1 blocker was found.

### Dependency direction

The project graph is acyclic. Relevant direct references remain in the prepared
direction:

- SharedKernel has no product-project dependency.
- Models depends on SharedKernel.
- AgentFramework Core depends on Models and SharedKernel.
- Persistence depends on Core, Models, and SharedKernel.
- MAF depends inward on Core/Models/SharedKernel and module-owned workspace contracts.
- Process Application depends only on Process Projections.
- Modules compose product/application/infrastructure dependencies; no lower layer
  references Blazor or a module implementation.

CodeAnalytics also reports three intra-project module cycles and two nested-type
cycles. This is not a “zero cycles” claim:

- the AgentFramework Hosting/module and Workbench module cycles match disclosed
  baseline debt;
- the Infrastructure module cycle appears only in the expanded full-solution scope
  and is not evidence of an SB05 project-reference regression;
- `ImageGenerationAgentRuntimeToolProvider` and its nested builder match disclosed
  baseline debt;
- `AgentReferenceDataCache` and its nested cache-entry implementation form the other
  reported type relationship cycle.

The nested implementation relationships do not reverse project ownership. They
remain visible debt rather than being relabeled as fixed.

### Partial-class policy

SB05 is a validation/proof phase and adds no production partial class or project
reference. It does not use a new partial file to hide runtime growth. The reviewed
runtime, provider, persistence, and process responsibilities remain in their prepared
owners.

### Construction and composition

- Provider contexts are factory-created and disposed per query.
- Process persistence remains scoped and sequential.
- immutable provider/preparation snapshots do not retain live agents, sessions,
  credentials, tools, authorization, approvals, or `DbContext`.
- runtime capability/tool/session objects remain per execution.
- no `BuildServiceProvider` or service-locator shortcut was introduced by SB05.

### Testability proof

- startup matrix: 5 repetitions × 4 scenarios;
- generic WAL crash matrix: 6/6;
- combined generic/chat/update WAL and regression matrix: 33/33;
- process snapshot/redaction group: 18/18;
- activity admission/profile isolation group: 11/11;
- storage scaling/usage group: 10/10;
- full serial solution build: 0 errors, 166 warnings.

The exact validation provenance is recorded in
`bundle://proof/SB05/transcripts/confirmed-validation-handoff.md`.

### Closure decision

The architecture gate passes with the three P2 follow-ups above. No project cycle,
shared-context parallelism, source-of-truth reversal, or fake extraction blocks UI
work. Any later evidence of stale cross-profile activity, unrecoverable WAL state, or
provider use after a conflicting cross-host change reopens SB05 and the owning
backend subbundle.
