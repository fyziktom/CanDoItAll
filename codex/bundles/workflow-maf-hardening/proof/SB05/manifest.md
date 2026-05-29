# SB05 Proof Manifest

## Scope

Runtime events, state, and checkpoint alignment.

## Changed File Hashes

- `ad162e4839bfff91f5803dc54dd1f511094dbae86edeebfa5970922cc56d1e98` `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `017aa211708cd3c7bcd51497add1d5e17fa133ef09d83bffc28c8345f76a7210` `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `cc8933123f35a50906175a0e0c1d0082848e7eca8bdff5f8de24ce0752c500f3` `repo://tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs`
- `896b9d9fdbd73c2bf9de7a684e822f16e640368796f13114d3d9bbd2598561b9` `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`

## Evidence

- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`
- Failing-first transcript: N/A - process hardening of runtime policy with targeted negative tests.
- Passing transcript: `bundle://proof/SB05/transcripts/proof-summary.txt`
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/proof-summary.txt`

## Cited Tests

- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorTests.MafBackendRecordsConfiguredFileArtifactsForCompletedFileWrites`
- Test name: `CanDoItAll.Tests.Unit.WorkflowExecutorTests.MafBackendRecordsFailedExecutorEventWithoutAmbiguousDataReflection`
- Test name: `CanDoItAll.Tests.Unit.WorkflowCatalogTests.RuntimeManagerRejectsInProcessWhenDurablePolicyDisallowsPreview`
- Test name: `CanDoItAll.Tests.Unit.WorkflowCatalogTests.TestRunnerReturnsRuntimeFailureForUnregisteredBackend`

## Invariants

- Invariant ID: `SB05-INV-001`
