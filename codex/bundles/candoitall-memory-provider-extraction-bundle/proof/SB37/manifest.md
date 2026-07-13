# SB37 Proof Manifest

## Status And Scope

- Status: Completed; SB40 real-host browser and E2E revalidation passed.
- Requirements: R09-R12, R20, R22-R24, R27.
- Semantic contract: `bundle://proof/SB37/semantic-invariants.md`.

## Artifact Index

- Failing-first N/A for this process reconstruction: no production pre-change executable transcript was retained and none is fabricated.
- Passing focused builds/tests: `bundle://proof/SB37/transcripts/reported-focused-validation.txt`.
- Passing terminal E2E/browser confirmation: `bundle://proof/SB40/transcripts/terminal-validation.txt` and `bundle://proof/SB40/transcripts/browser-validation.txt`.
- Boundary/partial/anti-stub audit: `bundle://proof/SB37/transcripts/source-and-anti-stub-audit.txt`.
- Before/after SHA-256 anchors: `bundle://proof/SB37/transcripts/file-hashes.txt`.
- Browser/screenshots: `bundle://proof/SB40/transcripts/browser-validation.txt`.
- Representative SHA-256 after hash: fb267202792cc9f2924e3640c538cebd8f86e827947f0795efc46415560441e5.

## Semantic Adequacy

- Shallow-pass trap: add mode fields in UI while runtime still queries a single/default/all registered providers or leaves the explicit memory directive in model text.
- Positive: typed settings plus dedicated parser/planner/fan-out/merge/tool/workflow/context owners passed focused 22/22, 29/29, and 24/24 suites.
- Negative: disabled/no-directive/unknown alias/disallowed provider/required failure cases are represented in focused suites and the SB40 real seam.
- Anti-stub: production types are independent top-level owners with no target stub markers or module/native dependency.
- Proof-depth disclosure: no standalone pre-change failing command was retained; SB40 therefore used fresh adversarial real-seam and red-team proof rather than reconstructing a baseline.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Typed memory settings/binding plan | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Memory/AgentMemoryAccessSettings.cs` | `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/AgentMemoryInvocationPlanner.cs` | focused configuration/invocation tests in `bundle://proof/SB37/transcripts/reported-focused-validation.txt` | no-directive/unknown/disallowed cases in the reported suite |
| Sanitized provider plan/context | `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/MemoryDirectiveParser.cs` | fan-out/merger/context contributor under `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory` | tool/workflow/context focused tests and `bundle://proof/SB40/transcripts/terminal-validation.txt` | unknown alias zero-dispatch and explicit query sanitization passed |

## Closure Decision

PASS. Focused proof plus SB40 browser and real contributor-handler-driver-ledger proof closes the terminal matrix.
