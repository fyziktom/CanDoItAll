# SB01 architecture snapshot

- Snapshot ID: `snap-20260727133012-194263b6`
- Created: `2026-07-27T13:30:12Z`
- Scope: nine projects, 762 source documents
- Refresh policy: forced refresh with dependency injection, persistence, risk, and Mermaid analysis enabled
- Result: no blocking analysis errors

## Project scope

- `CanDoItAll.SharedKernel`
- `CanDoItAll.AgentFramework.Models`
- `CanDoItAll.AgentFramework.Core`
- `CanDoItAll.AgentFramework.Persistence`
- `CanDoItAll.AgentFramework.Maf`
- `CanDoItAll.AgentFramework.Tooling`
- `CanDoItAll.Modules.AgentFramework`
- `CanDoItAll.Modules.Processes`
- `CanDoItAll.Modules.Workbench`

## Confirmed dependency direction

- `CanDoItAll.AgentFramework.Core` depends on `CanDoItAll.AgentFramework.Models`.
- `CanDoItAll.AgentFramework.Models` depends on `CanDoItAll.SharedKernel`.
- `CanDoItAll.AgentFramework.Persistence` depends on Core, Models, and SharedKernel.
- `CanDoItAll.AgentFramework.Maf` depends on Core, Models, Tooling, and SharedKernel.
- `CanDoItAll.Modules.AgentFramework` is the composition boundary for Core, Maf, Models, Persistence, Tooling, Workbench, and SharedKernel.
- `CanDoItAll.Modules.Processes` depends on Core, Models, Tooling, Modules.AgentFramework, and SharedKernel.
- `CanDoItAll.Modules.Workbench` depends on Core, Models, Tooling, and SharedKernel.

This supports the governed placement decision: generic bounded operational-stream storage belongs in SharedKernel; typed activity contracts belong in Models; lifecycle and read orchestration belong in Core; module authorization and composition remain in module projects.

## Findings relevant to SB01

- The highest-ranked maintainability findings are large-file and concentration warnings in the existing execution and module surfaces. They reinforce adding narrow collaborators and instrumentation calls instead of another execution partial or a broad refactor.
- No project-reference cycle or project-boundary finding blocks the proposed backend phases.
- The analyzer did report two pre-existing intra-project module/namespace cycles:
  - `CanDoItAll.Modules.AgentFramework.Hosting` ↔ `CanDoItAll.Modules.AgentFramework`.
  - `CanDoItAll.Modules.Workbench.CanvasAdapters` ↔ `CanDoItAll.Modules.Workbench`.
- The analyzer also reported one pre-existing intra-file type cycle between `ImageGenerationAgentRuntimeToolProvider` and its nested `ImageGenerationToolBuilder` in `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`. The owner constructs the builder and the builder retains its owner; SB01 neither changes nor relies on this cycle.
- These findings do not block the proposed placement, but they are baseline debt rather than a “no cycles” result. SB01 changes no production dependency and the post-backend snapshot must prove that no additional cycle is introduced.
- Exact analyzer node IDs, concrete symbol lookups, source paths, and tool correlation records are persisted in `bundle://proof/SB01/transcripts/codeanalytics-cycle-review.txt`.
- Existing `System.Security.Cryptography.Xml` `10.0.7` vulnerability warnings were reported by the solution and remain pre-existing, out-of-scope evidence. This bundle must not hide or relabel them as fixed.
- The snapshot is evidence for the baseline boundary map only. Later closure gates require a fresh post-change snapshot and dependency-diff review.

## Source references

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework`
- `repo://src/Modules/CanDoItAll.Modules.Processes`
- `repo://src/Modules/CanDoItAll.Modules.Workbench`
- `repo://src/Foundation/CanDoItAll.SharedKernel`
