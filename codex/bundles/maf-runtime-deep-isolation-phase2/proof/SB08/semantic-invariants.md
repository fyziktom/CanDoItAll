# SB08 Semantic Invariants

## INV-SB08-001 Runtime Is Not A Hidden Capability Namespace

- Source raw note: `avoid to add everything under mafagentruntime` and remove hidden builders/classes from partial runtime files.
- Expected behavior: capability configuration, composition, builders, hosted-agent construction, workspace helpers, input attachment preparation/support, execution option policy, tool-result classification, provider diagnostics, and process-artifact recovery are named top-level collaborators.
- Disallowed shallow implementation: only renaming files while leaving builders/DTOs nested under `MafAgentRuntime`, or keeping tests dependent on private runtime reflection.
- Failing-first proof: prior inventory in `bundle://proof/SB01/transcripts/nested-type-scan.txt` showed hidden nested runtime implementation classes.
- Passing proof: `bundle://proof/SB08/transcripts/source-boundary-scans.txt` returns a single `MafAgentRuntime.cs` file, no `partial class MafAgentRuntime`, no forbidden runtime-owned capability patterns, and no private nested runtime types.
- Changed source files and hashes: `bundle://proof/SB08/changed-file-hashes.md`.
- Production assertions: `RuntimeCapabilityComposer`, `MafRuntimeAgentFactory`, top-level builders, `InputAttachmentPreparer`, `InputAttachmentSupport`, `RequestScopedSessionContentScrubber`, `MafRuntimeExecutionOptionsResolver`, `MafRuntimeToolInvocationResultClassifier`, `ProviderRuntimeDiagnostics`, and `ProcessArtifactRecoveryService` are top-level runtime collaborators.
- Red-team negative case: architecture guards `MafAgentRuntime_is_not_a_split_partial_namespace` and `Runtime_tests_do_not_reflect_private_capability_composition_methods` reject split runtime partials and tests that keep private composition access on `MafAgentRuntime`.
- Downstream dependency check: `bundle://proof/SB08/transcripts/handoff-integration-tests.txt` proves handoff runtime behavior still works.

## INV-SB08-002 Behavior Parity Is Preserved

- Expected behavior: extracted collaborators preserve runtime tool composition, context contributors, workspace search, image model resolution, provider diagnostics, finalizer recovery parsing, and MAF handoff behavior.
- Disallowed shallow implementation: passing source scans while breaking runtime composition or recovery semantics.
- Passing proof: `bundle://proof/SB08/transcripts/focused-unit-tests.txt` passes 151 tests and `bundle://proof/SB08/transcripts/handoff-integration-tests.txt` passes 3 integration tests.
- Adversarial negative proof: finalizer tests reject in-progress artifacts and conflicting branch outcome keys after extraction.

## INV-SB08-003 Startup/Performance Boundary Does Not Regress By Eager Blocking

- Expected behavior: refactored runtime collaborators remain async/lazy and do not introduce sync-blocking calls in startup/composition paths.
- Disallowed shallow implementation: moving code into services that call `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, or `Thread.Sleep()`.
- Passing proof: `bundle://proof/SB08/transcripts/performance-boundary-check.txt` records command durations and no sync-blocking matches in extracted runtime paths.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Runtime capability composition | `RuntimeCapabilityComposer` | `MafRuntimeAgentFactory` | Per runtime build | Architecture guard and focused unit tests. |
| Hosted-agent construction | `MafRuntimeAgentFactory` | `MafAgentRuntime` public adapter | Per runtime build and handoff build | No-partial runtime guard and handoff integration smoke. |
| Input/session helper policy | `InputAttachmentSupport`, `RequestScopedSessionContentScrubber` | `MafAgentRuntime` run/session preparation | Per request and session persistence | Attachment tests target helpers directly. |
| Process-artifact finalizer recovery | `ProcessArtifactRecoveryService` | `MafAgentRuntime` finalizer recovery path | Provider-failure recovery | Adversarial branch-outcome tests. |
| Composition metrics | `RuntimeCapabilityComposer` | `IMafRuntimeCompositionMetrics` | Per composition stage | Performance-boundary transcript. |
