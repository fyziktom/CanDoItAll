# SB03 Proof Manifest

## Scope

MAF workflow compiler and executor foundation.

## Changed File Hashes

- `45f754d973baa064edd8676fbf98bcc4591bc25cc6b550a319a6a198a386603c` `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `017aa211708cd3c7bcd51497add1d5e17fa133ef09d83bffc28c8345f76a7210` `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `48f275a56f339bb3fffabc8bbf930283c8ca05a0b15e59d60a3e9f497b7e14ea` `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `dc6920946155d47436d05213587b6a15fa34a4048e7b0aa448f815e171ad7e76` `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `896b9d9fdbd73c2bf9de7a684e822f16e640368796f13114d3d9bbd2598561b9` `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`

## Evidence

- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript: N/A - process hardening of an existing runtime path with no production behavior released before passing proof.
- Passing transcript: `bundle://proof/SB03/transcripts/proof-summary.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/proof-summary.txt`

## Cited Tests

- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorTests.MafCompilerInvokesExecutorNodeThroughInvoker`
- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorTests.MafCompilerSkipsPredicateFalseBranch`
- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorTests.MafCompilerFanOutRoutesOnlySelectedTargets`

## Invariants

- Invariant ID: `SB03-INV-001`
