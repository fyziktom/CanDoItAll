# SB02 Architecture Snapshot

- Snapshot ID: `snap-20260727180924-829a813d`
- Created: `2026-07-27T18:09:24.390025+00:00`
- Solution: `CanDoItAll.slnx`
- Scope: six projects, 390 source documents
- Options: DI enabled, persistence analysis disabled, risk analysis enabled, Mermaid export disabled
- Tool status: snapshot/dashboard/dependency/service/symbol queries returned `ok: true`
- Durable tool transcript: `bundle://proof/SB02/transcripts/codeanalytics-post-repair.txt`

## Scoped projects

- `CanDoItAll.SharedKernel` — 21 documents, no project references.
- `CanDoItAll.AgentFramework.Models` — 73 documents, references SharedKernel.
- `CanDoItAll.AgentFramework.Core` — 127 documents, references Models and SharedKernel.
- `CanDoItAll.AgentFramework.Hosting` — 5 documents, references Core.
- `CanDoItAll.Modules.AgentFramework` — 118 documents, references Core, Hosting, Models, and SharedKernel.
- `CanDoItAll.Web` — 46 documents, references Modules.AgentFramework and SharedKernel.

The project direction matches the prepared contract: generic stream mechanics remain in
SharedKernel; immutable agent activity values remain in Models; lifecycle/admission/read
contracts remain in Core; authorization and profile-aware composition remain in the
module; Web consumes the module boundary.

## Dependency and cycle result

- No project-reference cycle was reported in the scoped snapshot.
- One module cycle was reported between
  `CanDoItAll.Modules.AgentFramework.Hosting`
  (`mod-proj-candoitall-modules-agentframework-candoitall-m-efe376421f64`) and
  `CanDoItAll.Modules.AgentFramework`
  (`mod-proj-candoitall-modules-agentframework-candoitall-m-f602d7c77eb2`).
- One type cycle was reported between
  `type-proj-candoitall-modules-agentframework-candoitall-m-5374bd3c4751` and
  `type-proj-candoitall-modules-agentframework-candoitall-m-a9e2e15d6c60`.

Those node ids match the SB01 baseline findings for the pre-existing module cycle and the
pre-existing `ImageGenerationAgentRuntimeToolProvider`/nested builder relationship. This
snapshot does not claim zero cycles.

The SB01 Workbench module cycle is absent only because Workbench was not in this
six-project scope. Its absence is not evidence that the baseline debt was fixed.

## Symbol confirmations

The snapshot directly resolves:

- `AgentExecutionActivityCoordinator` in
  `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`;
- required coordinator and workspace identity parameters on
  `AgentFrameworkWorkspaceService`;
- required coordinator/factory/profile accessor parameters on
  `AgentChatExecutionOrchestrator`;
- required coordinator on `CanDoItAllAgentWorkspaceFactory`;
- concrete coordinator plus profile-switch dependencies on
  `CurrentProfileAgentExecutionActivityReader`.

The service query resolves the scoped `IAgentExecutionActivityReader` registration at
`repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs:137`.
It also emits `DI0001` for factory registrations, so CodeAnalytics alone cannot prove the
singleton aliases. Source assertions and the 58/58 executable DI/semantic suite provide that
proof.

## Snapshot diagnostics and limits

- Dashboard health loaded all requested projects/documents.
- Diagnostics are warnings, including duplicate generated embedded-type names and
  workspace-load NU1903 advisories for `System.Security.Cryptography.Xml` `10.0.7`.
- Persistence analysis was not enabled, so persisted operation-id claims come from exact
  source and tests, not this snapshot.
- The snapshot is scoped and timestamped evidence, not an independent A2 decision.
- File hashes must be compared with the source state represented by the final review.
  If later production edits postdate this snapshot materially, the independent reviewer
  must request another refresh or explicitly constrain the accepted claim.

## Decision

Architecture evidence is present and supports the intended dependency direction.
The final independent A2 review accepted this snapshot and recorded `Pass`.
