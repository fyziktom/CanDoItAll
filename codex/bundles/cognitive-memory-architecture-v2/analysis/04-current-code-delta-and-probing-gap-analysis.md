# Current Code Delta And Probing Gap Analysis

## Inspection Scope

This update inspected the supplied architecture bundle and the supplied current CanDoItAll code snapshot. The review focused on whether the previously identified prerequisite boundaries are still gaps and what the new Interactive Memory Probing capability needs.

## Important Code Delta Found

The uploaded code already contains several prerequisite pieces that earlier architecture notes treated as required refactors.

### MAF Context Contribution Boundary Exists

Observed in the code snapshot:

- `src/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`

The code defines `IAgentContextContributor`, contributor descriptors, contribution policies, contribution results, and contribution trace collection. `MafAgentRuntime` attaches registered contributors in deterministic order. This is a good boundary for Cognitive Memory recall context injection and should be consumed rather than replaced.

### Source Snapshot Contracts Exist

Observed in the code snapshot:

- `src/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEvidenceSourceProvider.cs`
- `src/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeEvidenceSourceProvider.cs`

The code already exposes deterministic source snapshot providers for Workbench project structure, Process runtime evidence, and Workflow runtime evidence. These providers include source item ids, content hashes, provenance, permission context, redaction policy, layout metadata, links, references, storage references, paging/cursors, and provider versions.

### Relevant Integration Tests Exist

Observed tests include:

- `tests/CanDoItAll.Tests.Integration/WorkbenchSourceSnapshotIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/RuntimeEvidenceSourceIntegrationTests.cs`

They validate deterministic source ids/hashes, paging, cursor failure modes, redaction, restricted hash policy, and z-index extraction from Workbench metadata.

## Architecture Consequence

`subbundles/00-prerequisite-boundary-gate` should be treated as satisfied in the supplied code unless Codex finds a regression. Future implementation should not create competing source snapshot contracts with the same purpose. The Cognitive Memory module should adapt the existing `CanDoItAll.AgentFramework.Core` source snapshot records into its own durable source manifests/items.

## Naming And Contract Risk

The architecture bundle already has `contracts/csharp/SourceCanonicalizationContracts.cs` with a `MemorySourceItem` record inside `CanDoItAll.CognitiveMemory.Abstractions`. The live code has another `MemorySourceItem` in `CanDoItAll.AgentFramework.Core`. The namespaces differ, so this is technically viable, but it is easy for implementation agents to confuse them.

Recommended correction:

- Treat the live `CanDoItAll.AgentFramework.Core.MemorySourceItem` as the incoming snapshot DTO.
- Treat the Cognitive Memory persistent entity as a durable source record, preferably named `CognitiveMemorySourceItemRecord` or `MemorySourceItemRecordEntity` in the EF module.
- Add explicit adapter code instead of re-reading Workbench/Process/Workflow tables directly.

## Current Probing Gap

The architecture mentions knowledge probing as an Epistemic Drive input, but it does not yet define a complete probing subsystem. Missing pieces:

1. Durable probe sessions and turns.
2. Probe question generation strategies.
3. Probe answer assessment and confidence calibration.
4. User correction lifecycle.
5. Source challenge and "why do you think that" trace interaction.
6. Regression test generation from failed probes.
7. Probe evidence ingestion into Epistemic Drive.
8. UI for side-by-side answer, trace, source refs, confidence, gaps, and correction actions.
9. Workflow executors/tools for guided probing and learning validation.
10. Explicit safety rule that probing feedback is evidence, not automatic truth mutation.

## High-Value Additions Recommended

### 1. Cognitive Memory Dialogue Workbench

A dedicated UI mode where the user can talk to the memory module, inspect traces, confirm/correct answers, and generate review items or regression tests.

### 2. Probe Question Queue

A queue generated from Epistemic Drive, active project directions, stale knowledge, contradictions, weak coverage regions, and a controlled randomness budget.

### 3. Memory Regression Harness

Every important probe failure should be convertible into a durable test case with expected answer constraints, required source refs, forbidden context leakage, and pass/fail evaluation.

### 4. Confidence Calibration Ledger

Track cases where the system was confident but wrong, hesitant but correct, or unable to cite sources. This is necessary to improve recall scoring and answer confidence over time.

### 5. Context-Separation Challenge Probes

The Docker production/test/local/CI scenario should become a recurring probe fixture. The system must prove that similar topics are not merged into one truth.

## Corrected Implementation Priority

After source ingestion, recall trace, consolidation, and human review exist, implement Interactive Memory Probing before or alongside full Epistemic Drive rollout. Epistemic Drive can create basic learning proposals without probing, but it becomes much stronger when probe outcomes exist as evidence.
