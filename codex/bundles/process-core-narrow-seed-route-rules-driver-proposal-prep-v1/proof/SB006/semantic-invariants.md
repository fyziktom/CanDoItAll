# SB006 Semantic Invariants

- Invariant ID: `SB006-CORE-DEPENDENCY-CLEAN`
- Source raw note: Only create Process Core when a narrow, justified cutline exists.
- Expected behavior: Core references Contracts only and contains no package references or infrastructure dependencies.
- Disallowed shallow implementation: Adding Core while letting it depend on Modules, EF, Workspace, Storage, AgentFramework, MAF, or driver abstractions.
- Failing-first test: N/A process/no production behavior; negative source scans cover the forbidden dependency surface.
- Passing test: bundle://proof/common/transcripts/unit-architecture.txt
- Changed source files: repo://src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj and repo://src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeEnums.cs
- Production assertions: Core exposes pure route data and rules only; runtime orchestration remains application-local.
- Red-team negative case: bundle://proof/common/transcripts/core-forbidden-scan.txt rejects forbidden Core dependency tokens.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves the solution compiles with the new project reference.
