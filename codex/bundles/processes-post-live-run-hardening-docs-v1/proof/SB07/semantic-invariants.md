# SB07 Semantic Invariants

## Invariants

- Invariant ID: SB07-INV-001
- Source raw note: RN07 - Harden selected-run manager chat resolution and inspection context.
- Expected behavior: Manager chat resolves the technical manager through one shared resolver that reports reason code, confidence, summary, and candidate diagnostics for configured managers, selected-run assignments, and fallback candidates before dispatching chat.
- Disallowed shallow implementation: Prompt-only wording, docs-only behavior, source-only proof for runtime behavior, UI-only hiding of errors, duplicate private resolver helpers in manager chat services, silent fallback after ambiguous candidates, or hardcoded Blazor/Tetris/project/run/user paths in production code.
- Failing-first test: bundle://proof/SB07/transcripts/failing-first.txt proves the old duplicate resolver helpers are absent and selected-run/fallback ambiguity cases are blocked.
- Passing test: bundle://proof/SB07/transcripts/passing.txt proves the targeted resolver and manager dispatch compatibility tests pass.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs; repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatModels.cs; repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs; repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs; repo://src/CanDoItAll.Modules.Processes/README.md; repo://tests/CanDoItAll.Tests.Integration/ProcessObservationIntentResolverTests.cs.
- Production assertions: `ProcessManagerChatService` and `ProcessWorkspace.ManagerChat` use `ProcessManagerAgentResolver` results instead of local scoring, attach reason/confidence/summary to manager prompts and metadata, and block ambiguous manager resolution before chat dispatch.
- Red-team negative case: Equal top selected-run assignment candidates or equal top fallback manager options cannot select an arbitrary manager and cannot fall through to a weaker text signal.
- Downstream dependency check: SB10 can rely on explicit agent-manager resolution diagnostics, SB13 can expose operator inspection context, and SB18 must keep ambiguity rejection in final release-readiness checks.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Typed manager-agent resolution | repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs `ResolveConfiguredManager`, `ResolveAssignedManager`, and `ResolveFallbackManager`; source proof bundle://proof/SB07/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs and repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs | bundle://proof/SB07/transcripts/passing.txt proves reason-code resolution and fallback preference behavior through targeted integration tests | bundle://proof/SB07/transcripts/failing-first.txt proves selected-run and fallback ambiguity is rejected |
| Manager chat resolution context | repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs and repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs prompt and metadata assembly | Agent Framework invocations, manager chat projections, and downstream operator inspection | bundle://proof/SB07/transcripts/source-assertions.txt proves reason/confidence/summary fields are wired into prompt context and metadata | bundle://proof/SB07/transcripts/passing.txt proves unresolved or ambiguous managers do not dispatch through compatibility paths |
| Manager resolution governance docs | repo://src/CanDoItAll.Modules.Processes/README.md | SB10, SB13, and SB18 follow-up subbundles | bundle://proof/SB07/transcripts/source-assertions.txt proves docs name the shared resolver boundary and ordered configured/selected-run/fallback semantics | bundle://proof/SB07/transcripts/anti-stub-audit.txt proves the docs are not placeholder closure |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB07/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB07/transcripts/passing.txt.
- Source assertions: bundle://proof/SB07/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB07/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB07/transcripts/changed-file-hashes.txt.
