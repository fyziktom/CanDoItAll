# SB07 Proof Manifest

## Status

Completed.

## Goal

Harden manager chat selected-run resolution and inspection context with a shared typed resolver that reports reason codes, confidence, candidate summaries, and ambiguity diagnostics.

## Changed Files

| File | Purpose | Hash proof |
| --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs | Replaces duplicated manager-agent selection logic with typed configured, selected-run assignment, and fallback resolution results. | bundle://proof/SB07/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatModels.cs | Extends manager chat projections with resolution reason code, confidence, and summary. | bundle://proof/SB07/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs | Uses the shared resolver for manager chat loading and sends reason/confidence/summary into prompt context and invocation metadata. | bundle://proof/SB07/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs | Uses the shared resolver for selected-run manager chat and blocks ambiguous manager choices before dispatch. | bundle://proof/SB07/transcripts/changed-file-hashes.txt |
| repo://src/CanDoItAll.Modules.Processes/README.md | Documents manager resolution ownership and the reason/confidence/candidate-summary boundary. | bundle://proof/SB07/transcripts/changed-file-hashes.txt |
| repo://tests/CanDoItAll.Tests.Integration/ProcessObservationIntentResolverTests.cs | Adds reason-code, ambiguity, and fallback-preference tests for the shared resolver. | bundle://proof/SB07/transcripts/changed-file-hashes.txt |

## Failing-first Or Adversarial Proof

- bundle://proof/SB07/transcripts/failing-first.txt records a non-zero search proving the old duplicate manager-resolution helpers are absent from `ProcessManagerChatService` and records adversarial ambiguity tests for selected-run assignments and fallback manager options.

## Passing Proof

- bundle://proof/SB07/transcripts/passing.txt records 7 passing targeted integration tests for `ProcessObservationIntentResolverTests` and existing dispatch resolver compatibility coverage.

## Source Assertions

- bundle://proof/SB07/transcripts/source-assertions.txt records the typed resolution record, reason-code enum, resolver call sites, prompt/metadata fields, resolver tests, and README ownership docs.

## Anti-stub Audit

- bundle://proof/SB07/transcripts/anti-stub-audit.txt records no TODO, pending, stub, or `NotImplementedException` markers in the SB07 changed runtime, test, and README files.

## Changed-file Hashes

- SHA-256 `4C801448B3509682879E02B9AF772F8AB1F9F050A5E2C2165CE364481AACF8F4` repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs
- SHA-256 `423A06BD0478DBB1F9EFEA120E79BBD99BC39E11D8C2B9982AA95BDD3131CC59` repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatModels.cs
- SHA-256 `82BAEB518258CEB012072FE9E1D8AA6252AA9D3F5FE4F78B9F2469861FAD8480` repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs
- SHA-256 `48412B446C2DE2B1F865870422C5244DDA6D29E717CD728A7D44B884C59B463D` repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs
- SHA-256 `877918764E0302CB91A315CAAF9E33519B05D7D5F67D16EBEE63DB8DD0C99F72` repo://src/CanDoItAll.Modules.Processes/README.md
- SHA-256 `3A8E17D87B1A4F55639E212877320CC23F0DABF746DCBB294B896F5D5CC0CAFB` repo://tests/CanDoItAll.Tests.Integration/ProcessObservationIntentResolverTests.cs
- bundle://proof/SB07/transcripts/changed-file-hashes.txt records the command transcript for these hashes.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Typed manager-agent resolution | repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs via `ProcessManagerAgentResolution`; source proof bundle://proof/SB07/transcripts/source-assertions.txt | repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs and repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs | Evaluates configured manager options first, selected-run assignments second, and fallback candidates last; returns reason code, confidence, summary, and candidates before any manager chat dispatch | Equal top selected-run or fallback candidates return ambiguity instead of a silent fallback; adversarial proof bundle://proof/SB07/transcripts/failing-first.txt |
| Manager chat resolution context | repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs and repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs | Agent Framework manager invocations and manager chat projections | Adds resolution summary, reason code, and confidence to projection, prompt context, and invocation metadata for selected-run inspection | Ambiguous or unresolved manager resolution blocks chat dispatch with an explicit resolution error; proof bundle://proof/SB07/transcripts/passing.txt |
| Manager resolution governance docs | repo://src/CanDoItAll.Modules.Processes/README.md | Downstream SB10 agent-skill matrix, SB13 observability, and SB18 red-team checks | Documents the shared resolver boundary and the configured/selected-run/fallback ordering used by runtime manager chat | Anti-stub and source assertion proof prevents prompt-only or docs-only closure; bundle://proof/SB07/transcripts/anti-stub-audit.txt |

## Browser Validation

N/A. SB07 changed manager chat runtime resolution, projection metadata, prompt context, tests, and README documentation. It did not change Razor markup, CSS, route wiring, layout, or visible UI rendering components.

## Closure

- SB07-INV-001 is satisfied by repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs and bundle://proof/SB07/transcripts/passing.txt.
- Ambiguous selected-run and fallback manager choices are rejected by bundle://proof/SB07/transcripts/failing-first.txt.
- Shared manager chat resolution context is recorded in bundle://proof/SB07/transcripts/source-assertions.txt.
- SB10 and SB13 may rely on manager ambiguity proof after this gate.
