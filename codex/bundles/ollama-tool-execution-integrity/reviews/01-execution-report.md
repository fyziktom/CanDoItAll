# Execution Report

## Status

- Execution state: `Complete`
- Bundle state: `Complete`
- Product defect: solved for direct and shared Ollama routes
- Original incident run: `894e1404-3019-4221-8be6-7769c0f472ae`, preserved
- Original 5032 host: stopped throughout implementation and final validation
- Disposable 5042 host: stopped after live and browser proof

## Result

The application now treats tool execution as typed evidence rather than assistant prose. A mutating call completes successfully only when its trusted invocation outcome is `Succeeded` and its effect state is `Committed`. Malformed arguments are rejected before delegate execution with bounded, redacted correction feedback. Durable receipts preserve outcome, effect and correlation, and the next turn receives only currently authorized evidence from the matching session, agent, profile and project scope.

Project-structure asset creation reports `Committed` only after durable storage succeeds. Matching committed project effects refresh the open canvas from canonical state. Effects for another project, unknown outcomes and failed or uncommitted calls do not refresh it.

MAF was upgraded to 1.20.0 with its coherent dependency family. The upgrade improves the supported baseline but did not solve the incident by itself. Live shared Ollama exposed two additional adapter defects that the implementation corrects narrowly: boolean JSON Schema nodes are normalized only on Ollama-bound inference payloads, and the shared source accepts an assistant message whose empty content accompanies valid `tool_calls`.

## Principal Changes

| Boundary | Implemented behavior |
| --- | --- |
| MAF/tool adapter | Pre-execution schema validation, safe binding feedback, typed outcome/effect/correlation and trace preservation |
| Core execution | Evidence-based completion, exact-operation recovery and scoped prior-tool evidence projection |
| Persistence/API | Durable typed tool receipts with legacy `Unknown` compatibility and redacted public mapping |
| Workbench | Durable project-asset commit evidence, separated response projection/analytics and matching-project refresh |
| Shared provider | Full direct/shared tool sequence validation, Ollama-only recursive boolean-schema normalization and valid empty assistant content with tool calls |
| Packages | Stable MAF 1.20.0, A2A/Hosting preview 1.20.0-preview.260831.1, MEAI 10.9.0 and Microsoft.Extensions 10.0.11 floor |

## Validation

| Gate | Selection reason | Result | Evidence |
| --- | --- | --- | --- |
| Focused unit | MAF 1.20 compatibility, feedback/classification and shared relay policy | 154/154 passed | `bundle://proof/final-focused-unit.log` |
| Focused integration | completion, receipts, scoped evidence, parity, project effects and end-to-end flow | 37/37 passed | `bundle://proof/final-focused-integration.log` |
| Focused components | matching committed-effect refresh lifecycle | 5/5 passed | `bundle://proof/final-focused-components.log` |
| Production Release build | root package graph and cross-cutting production changes | Passed, 0 warnings and 0 errors | `bundle://proof/final-production-build.log` |
| Stable test-solution Release build | frozen stable test assemblies | Passed, 0 warnings and 0 errors | `bundle://proof/final-stable-build.log` |
| Frozen broad stable gate | package graph, public receipt persistence and composition invalidated the full stable surface | 9,525/9,526 passed in one run; the only failure was an unrelated concurrent-search duration threshold | `bundle://proof/stable-gate.log` |
| Exact unrelated timing rerun | distinguish a duration flake from a product regression | 1/1 passed in 6 seconds | `bundle://proof/provider-history-timing-rerun.log` |
| Live direct Ollama | actual installed model, real MAF/runtime/persistence/project/UI flow | Succeeded/Committed; canvas 3 to 4 nodes without reload | `bundle://proof/SB06/live/direct-ollama-live-summary.json` |
| Live shared Ollama | actual shared-provider publication/import/source relay and same model | Succeeded/Committed; canvas 4 to 5 nodes without reload | `bundle://proof/SB06/live/shared-ollama-live-summary.json` |
| Portability/static, documentation and completed-bundle gates | required final repository closure | Passed | `bundle://proof/final-portability-static.log`; `bundle://proof/final-documentation.log`; `bundle://proof/final-bundle-validation.log` |

The focused integration build emitted one pre-existing xUnit analyzer warning in `FileSandboxWorkspacePreparedCommitReadIntegrationTests.cs`. The final production and stable test-solution builds completed with zero warnings and zero errors.

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| SB00 | Prepared 1.18 graph and isolated 1.20 characterization remained current | MAF 1.20 compatibility, Release build and portability proof passed | SB01-SB06 focused and live flows | Completed | Upgrade alone did not resolve malformed binding |
| SB01 | SB00 completed | INV01/INV02 manifest, failing-first, passing, source, anti-stub and downstream proof passed | SB02, SB03, SB05 and SB06 | Completed | Eight feedback cases and real production-path smoke |
| SB02 | SB01 trusted outcomes completed | Completion and durable receipt/API positive and negative cases passed | SB03, SB05 and SB06 | Completed | Exact-operation recovery; unknown remains unresolved |
| SB03 | SB01/SB02 evidence lifecycle completed | INV03 manifest, scope adversaries, source, anti-stub and downstream proof passed | SB04 and SB06 | Completed | Evidence recomputed under current authorization |
| SB04 | SB01-SB03 completed | Direct/shared parity, connector/policy regressions and both live routes passed | SB05 and SB06 | Completed | Reopened twice for observed shared-route defects, then reclosed |
| SB05 | SB01-SB04 completed | Commit/readback, notification filtering and inspected browser refresh proof passed | SB06 | Completed | No markup or component API change |
| SB06 | SB00-SB05 completed | Deterministic end-to-end, live direct/shared, browser and final red-team proof passed | Final closure | Completed | Original incident preserved; disposable host stopped |

Later live shared findings reopened SB04 twice: first for Ollama boolean-schema compatibility and then for empty assistant content with tool calls. Each fix received focused regression coverage, both final builds, and a successful full shared run before SB04–SB06 were reclosed.

## Live Direct Route

- Project: `12e2e906-1325-464b-92f4-67e1774fbf9a`
- Agent: Portfolio Architect `952b041a-aba0-385b-8e4e-494c4b21d831`
- Model: `gemma4-12b-256k`
- Run: `a1e4d57b-b84b-4eb1-8cea-432cddf13861`
- Chat: `28e804b9-6b4b-42e2-adc7-e554818e4f69`
- Asset receipt: `Succeeded` / `Committed`
- Visible result: `Direct Ollama Schema Fixed`

## Live Shared Route

- Source: `8b90f608-3de9-4453-8a4e-b9d2ab90d714`
- Publication: `ff8d0481-2e78-4341-8aab-852c8eeaed2f`
- Imported provider: `15c5c219-3615-4628-b954-67121ec86355`
- Opaque routing model: recorded in `bundle://proof/SB06/live/shared-ollama-live-summary.json`
- Run: `b1b2ead6-09bc-4248-b007-d4bb74cfa30c`
- Chat: `ee82514d-2659-497a-95a7-711e6c1d604a`
- Tool sequence: one workspace write and one project-asset create
- Asset receipt: `Succeeded` / `Committed`
- Visible result: `Shared Ollama Schema Fixed`

## Browser Artifacts And Visual Review

| State | Artifact | Inspected finding |
| --- | --- | --- |
| Incident input | `bundle://inputs/reported-state.png` | Assistant reported a retry while the requested node remained absent; it is evidence of the original symptom, not proof of a refresh defect. |
| Normal canvas | `bundle://proof/SB06/live/ollama-committed-canvas-normal.png` | 2048×1100 viewport and document; five canonical nodes are represented, including both final direct/shared nodes. The primary canvas remains visible in the first viewport, selection details stay in the existing right panel, and no document-level scrolling or clipping was observed. |
| Direct contextual chat | `bundle://proof/SB06/live/ollama-schema-fixed-committed-refresh.png` | The completed direct run and newly visible node coexist without reload; transcript scrolling remains within the chat. |
| Shared contextual chat | `bundle://proof/SB06/live/ollama-shared-schema-fixed-committed-refresh.png` | The completed shared run and newly visible node coexist without reload; contextual chat and conversation list are supporting overlays while the canvas remains visible. |

No markup or component-library contract changed. The existing canvas owns graph navigation; selection/chat panels own their internal scrolling. The normal and relevant open-overlay states were inspected at the desktop target. Mobile validation is outside this app’s declared scope.

## Governed Proof

- SB01: `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md` cover INV01 safe pre-execution feedback and INV02 trusted mutation outcomes.
- SB03: `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md` cover INV03 authorized canonical cross-turn evidence.
- Both manifests cite failing-first evidence, passing transcripts, source assertions, anti-stub audits, production artifact lifecycle matrices and downstream checks.
- Final verifier: `bundle://proof/red-team-closure.md`.

## SB01 Semantic Adequacy Evidence

- Raw note owned: the captured agent used an incorrect project-asset signature, received opaque feedback and claimed it had added a node that was absent.
- Shipped behavior: malformed arguments execute no delegate and return bounded redacted field feedback; only trusted `Succeeded/Committed` evidence can complete a mutation.
- Source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafToolArgumentBindingFailureMapper.cs` and `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentToolCompletionAssessment.cs`.
- Test proof: `bundle://proof/SB01/transcripts/tool-argument-feedback-passing.log` and `bundle://proof/SB01/transcripts/production-path-downstream-smoke.log`.
- Shallow-pass trap: trusting assistant prose, any non-null result or an improved description would still permit false mutation success.
- Adversarial negative proof: secret-bearing malformed/type calls execute zero delegates, unknown mutation shapes cannot certify a commit, and unrelated operations cannot recover the failure; see `bundle://proof/SB01/semantic-invariants.md`.
- Semantic positive proof: a corrected nested asset call reaches the authorized delegate once, persists a committed receipt and creates exactly one canonical node in `bundle://proof/SB01/transcripts/production-path-downstream-smoke.log`.
- Anti-stub audit: no TODO, NotImplemented, fixture-specific branch or template-only production path remains; see `bundle://proof/SB01/transcripts/anti-stub-audit.log`.

## SB03 Semantic Adequacy Evidence

- Raw note owned: the model needed reliable prior failure evidence on the correction turn with equivalent behavior above direct and shared endpoints.
- Shipped behavior: Core recomputes bounded typed evidence only for the matching current session, agent, database profile, project/source and present authority, then the neutral MAF adapter carries it across provider routes.
- Source proof: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentToolEvidenceProjection.cs`, `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` and `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`.
- Test proof: `bundle://proof/SB03/transcripts/scoped-prior-tool-evidence-passing.log` and `bundle://proof/SB01/transcripts/production-path-downstream-smoke.log`.
- Shallow-pass trap: replaying provider-session prose or seeding a magic system message would leak stale authority and would diverge after a provider switch.
- Adversarial negative proof: foreign project/session/agent/profile, revoked authority, model-authored fake evidence and flood ordering are rejected in `bundle://proof/SB03/transcripts/scoped-prior-tool-evidence-passing.log`.
- Semantic positive proof: a matching prior failure appears on the next authorized turn and remains identical through direct/shared route switching; see `bundle://proof/SB03/semantic-invariants.md`.
- Anti-stub audit: no TODO, NotImplemented, fixture-specific branch or template-only production path remains; see `bundle://proof/SB03/transcripts/anti-stub-audit.log`.

## Cleanup

The Portfolio Architect was restored to its direct provider. The imported shared profile was retired, the shared source was disabled, and the direct provider was unpublished; the final public catalog contains zero providers. The application exposes no source-delete action while retained audit history still references the disabled source.

Deletion of the disposable live-proof project was attempted through the verified project API. Automatic approval review rejected that destructive action because it could not verify explicit authorization to delete the project. The project therefore remains as retained evidence. No workaround was used.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| N01 inspect the incident | Solved | `bundle://analysis/public-run-evidence.json`; original run preserved |
| N02 stop 5032 during work | Solved | `bundle://analysis/host-stop.json`; final port check |
| N03 absent node and false success | Solved | typed outcomes/completion plus deterministic and live direct/shared proof |
| N04 automatic refresh | Solved | 5 component cases plus normal/direct/shared browser evidence |
| N05 smaller Ollama tool correctness | Solved | safe correctable feedback, Ollama schema normalization and live `gemma4-12b-256k` runs |
| N06 direct/shared independence | Solved | `bundle://proof/final-focused-integration.log` and `bundle://proof/SB06/live/shared-ollama-live-summary.json` |
| N07 deep C# and filesystem feedback audit | Solved | `bundle://reviews/csharp-architecture-gate.md`, `bundle://proof/SB01/manifest.md` and `bundle://proof/SB03/manifest.md` |
| N08 prepare bundle, then implement when authorized | Solved | preparation history retained; all SB00–SB06 complete after explicit authorization |
| N09 use attachment as evidence | Solved | `bundle://inputs/reported-state.png` retained and distinguished from instructions |
| N10 assess MAF 1.20 and completion behavior | Solved | `bundle://analysis/03-maf-1-20-assessment.md`, upgraded package graph and truthful completion tests |

## Residual Risk

A smaller model can still generate malformed arguments. The shipped behavior now keeps that failure non-mutating, explicit, redacted and available for a corrected retry; it does not claim a commit. The disposable proof project remains because automated approval review rejected deletion. The broad stable suite’s unrelated timing threshold is documented with an immediate exact-case pass rather than hidden or rerun broadly.
