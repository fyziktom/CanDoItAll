# Workflow authoring WF1

Execution of `codex/bundles/CanDoItAll_Workflow_Authoring_WF1/prompt.md` on the
existing `components-decoupling` branch. The sealed package and historical bundles
are unchanged. This is an in-progress implementation record, not family closure.

## Entry and progression

| Stage | State | Next concrete work |
|---|---|---|
| S0 | CA1-R1 reproduced and repaired; focused checks pass | Signed checkpoint |
| W1 | Source census in progress | Native lossless document reproduction and state boundary |
| W2 | Pending | Complete actual canvas, floating windows, inspectors and settings |
| W3 | Pending | Template, both preview inputs, run/event dialogs and native composition |
| W4 | Pending | Native consumers, source/publish desktop, final images and closure gates |

Entry source pair: main `28ca64aaf0a2e3f65ee44feae2d931852bd18611`, Components
`b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`, FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`; all three worktrees were clean.
Main adds only the sealed WF1 package over reviewed `ccca2fd3a`. SDK is 10.0.303;
isolated build configuration is `WF1`, with evaluated sibling-source resolution.
CI separately pins FileTools `498b36825bd5a5222429972af120b04becf4b3f6`.

Configured RSA signing identity `96E836FAA8854EE98ABC10903C206549E1D7EAD6`
successfully signed and verified a harmless probe through the existing GPG agent.
The local Components Tooltip commit verifies with that identity. A read-only remote
query still reports development `4a858412d2c2a3f6123bf23d8c4584f05b47627d`.
The fix is local-only; no remote delivery is claimed. The freshly built capability
sandbox's BaseLib assembly matches the sibling output byte-for-byte, SHA-256
`4e6717da01f99be459f414a65567b2e0e3777699061038fdeaf4269472f85acb`.
Final loaded-browser asset verification remains part of W4.

CodeAnalytics, Components and dotnetwatch MCP methods are unavailable in this
session. The authorized fallback is current source search, evaluated MSBuild
references, CLI builds/watch and real Playwright. No unavailable MCP is reported as
executed. SharedInfo tooling and Git standards were read; repository testing, CI
and UI seams remain authoritative. The execution, component-composition and C#
architecture skill gates apply to subsequent extraction stages.

## S0: setup evidence attribution

The real `CapabilityAuthoringForm` reproduced stale diagnostics after a successful
attempt followed by an unknown acknowledgement. The first selection discovered and
executed 12 cases: 6 failed, 6 passed, 0 skipped. A second unchanged-production
attempt put the actual rendered diagnostic assertion first and again recorded
6 failures / 6 passes. Both original attempts are retained privately.

`CapabilityAuthoringSession.TestSetupAsync` now clears diagnostic/tool evidence when
the current attempt becomes unknown. It retains the unknown-effect guard, draft and
accepted catalog facts. The notification directs the operator to inspect the native
result rather than suggesting a blind retry. No runtime setup owner changed.

The regression drives the actual form for Tool and MCP: same revision, A to B,
A to B to A, real unsuccessful results, edits during an outstanding acknowledgement,
late success/error after close or owner retirement, and independent editors. It checks
session facts and rendered text, no setup on Save, and no duplicate setup admission.
The controlled test boundary simulates acknowledgements; it does not replay unknown
native setup effects or claim new live-provider proof.

Affected production build: `dotnet build
src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI -c WF1 /m:1` passed.
The owning test project is
`tests/Components/CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests`.
Build-backed discovery confirmed the expected 28 cases for
`FullyQualifiedName~CapabilitySetupAttributionTests|FullyQualifiedName~CapabilityAuthoringStateTests|FullyQualifiedName~CapabilityAuthoringFormTests`.
Execution with the identical filter, `-c WF1 --no-build --no-restore /m:1`, passed
28/28, with no failures or skips. This includes 14 new cases and 14 retained
state/form cases. Existing AngleSharp NU1902 warnings remain qualified.

Portability tooling self-tests passed 6 and 4 cases. The fresh tracked-source scan
covered 8,387 files and 34,085 findings; enforcement passed with 15,205 reviewed
executable-source findings unchanged, without baseline-write mode. No protected
production file was added in S0. Final WF1 source requires a new full scan.

Private artifacts are under `artifacts/workflow-authoring-wf1`: original and final
TRX/logs, signing probe, evaluated references and the complete scan. Historical CA1
mixed Stable results and subsequent focused dispositions retain their original scope;
the S0 checks do not reinterpret them as a clean broad run.

## Planned boundary and validation

The primary authoring surface remains the actual CanvasWorkbench with its three
floating windows and nested inspectors. Supporting information stays in its existing
tabs/dialogs, with compact counts. Canvas owns its viewport; wide/full dialogs retain
their own scrolling body and visible action footer. All new browser checks use
1920 by 1080 at scale 1.

The existing `Workflows.UI` shell retains its ASP.NET/BaseLib/Charts-only graph.
A separate `WorkflowAuthoring.UI` owns actual rendering, local graph editing and
lossless presentation state; native module adapters retain catalog, Prompt,
component, trust, profile and execution authority. Its independent sandbox must
compose the same children without production DI. Adding the native canvas graph to
the old shell or merely moving injections into another partial does not satisfy this
boundary. W1 must settle the exact dependency closure before the extraction.

Focused selection is the stage default. One final frozen broad Stable checkpoint
will be selected if new public authoring contracts, composition or test-solution/CI
registration changes invalidate the prior aggregate. No broad S0 run is justified.
Native PostgreSQL, source/two-client images, file approvals, Scheduler, held responses,
accepted/incomplete LLM output and History journeys remain pending. Existing retained
containers and ordinary port 5032 are outside this task's fixture ownership.
