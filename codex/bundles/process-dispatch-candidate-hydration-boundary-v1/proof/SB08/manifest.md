# Header Selector And Snapshot Parity Proof Manifest

## Changed File Hashes

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHeaderSelector.cs` SHA-256 `6dfbe311039f23e94e315c52f91d7fcc4302526c96d0c0d85415438f04bb0e63`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs` SHA-256 `c680abd24af3404d0cd85ec39749184ab873993dba61601a13c2f7dd7c63222b`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` SHA-256 `b97a3548311d1a5645830e38ff9e2dd0c88f020cc0f53a0d870ae6cb3aa1616f`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` SHA-256 `70a866b2ef7b6faf0e22d47ca0101791b7b5244175d62403167be48671c0d3f4`

## Command Transcripts

- Failing-first: `proof/SB08/transcripts/sb08-failing-first-selector-snapshot-trap.txt`
- Passing transcript: `proof/current/transcripts/candidate-hydration-architecture-tests.txt`
- Anti-stub audit transcript: `proof/current/transcripts/candidate-hydration-anti-stub-and-scope-scan.txt`
- Source assertion: `proof/SB08/source-assertions/gate-b-header-snapshot-parity.md`
- Semantic invariant contract: `proof/SB08/semantic-invariants.md`
- Bundle reference: `bundle://proof/SB08/manifest.md`

## Source Assertions

- Raw note owned: preserve original candidate header selection and hydration readback behavior.
- Shipped behavior: header selection delegates to ProcessDispatchCandidateHeaderSelector.SelectAsync; hydration readback delegates to ProcessDispatchCandidateHydrationLoader.LoadAsync without moving side effects.
- Shallow-pass trap: a shallow extraction where inline query logic remains in the dispatcher or the loader performs writes, workflow execution, or technical-agent binding.
- Semantic positive proof: `proof/current/transcripts/candidate-hydration-architecture-tests.txt`
- Adversarial negative proof: `proof/SB08/transcripts/sb08-failing-first-selector-snapshot-trap.txt`
- Anti-stub audit: `proof/current/transcripts/candidate-hydration-anti-stub-and-scope-scan.txt`
- Downstream dependency check: Unlocks artifact, branch, and assignment assembly movement in SB09-SB12.
