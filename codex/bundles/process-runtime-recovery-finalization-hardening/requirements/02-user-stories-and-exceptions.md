# User Stories And Exceptions

## Actor Stories

| ID | Actor | Story | Expected system behavior | Failure or escalation path | Owner |
|---|---|---|---|---|---|
| US01 | Step agent | As an agent, I can re-fetch my current step contract after context compression. | Tool-backed contract returns instruction, required inputs, expected outputs, branch outcomes, required receipts, and finalization rules. | If unavailable, step blocks with manager-visible runtime diagnostic. | SB03 |
| US02 | Step agent | As an agent, I can read every required connected artifact for my step without guessing filenames. | Runtime exposes concrete artifact refs grouped by slot and source step. | Missing concrete ref routes to upstream repair or manager action. | SB02, SB05 |
| US03 | Step agent | As an agent, I can finish only after my expected output artifacts are produced. | Finalization gate verifies managed artifact write/readback and produced slot mapping. | Missing own output returns current-step repair only if the step can write it and no upstream input is missing. | SB04, SB05 |
| US04 | Manager | As a manager, I can see whether a blocker is missing input, denied access, missing tool, transient provider error, child run block, or instruction non-compliance. | Recovery taxonomy records typed category, responsible owner, and next action. | Unknown category becomes manager-required diagnostic, not automatic retry. | SB05 |
| US05 | Manager | As a manager, I can send work back to the responsible previous step when connected artifacts are missing. | Upstream repair router selects producer step from artifact lineage and requests rework with required output contract. | If producer cannot be determined, manager gets explicit unresolved lineage blocker. | SB02, SB05 |
| US06 | Downstream agent | As a downstream agent, I get bounded artifact context rather than all changed product files. | Context package includes manifests, summaries, and retrieval handles; full file content requires explicit driver policy. | Oversized package blocks packaging with actionable policy diagnostic. | SB07 |
| US07 | Template author | As a template author, I can define connected artifacts and finalization expectations generically. | Template/kernel builder preserves artifact connection semantics into runtime plan and step contract. | Invalid connection fails template or plan validation before run. | SB02 |
| US08 | Process driver author | As a driver author, I can define domain-specific completion and recovery policy. | Driver contracts expose finalization and evidence policy without changing generic runtime. | Missing driver policy uses explicit unsupported-policy diagnostic. | SB06 |
| US09 | Operator | As an operator, I can distinguish retryable transient failure from useless retry. | Same-step retry only occurs when retry safety, idempotency, and current-step ownership are all proven. | Repeated or unsafe cases stop at manager decision. | SB05 |
| US10 | QA agent | As QA, I can verify that runtime advancement used production paths. | Proof covers launch, dispatch, adapter conversion, artifact ledger, manager handoff, and projection. | Manually seeded positive state is rejected for critical proof. | SB08 |

## Exception And Escalation Matrix

| ID | Situation | Correct routing | Must not do | Required proof |
|---|---|---|---|---|
| EX01 | Required input artifact slot has no concrete artifact ref. | Manager or runtime requests upstream producer rework. | Retry consumer step. | Negative test where consumer stays blocked and producer is selected. |
| EX02 | Required artifact exists but storage/readback fails. | Manager action or storage repair diagnostic, depending on failure type. | Mark slot available as enough. | Test proves finalization checks concrete read ref. |
| EX03 | Artifact is produced by earlier non-direct step. | Consumer resolves source step through connection lineage. | Assume direct previous step only. | Test with A produces, B unrelated, C consumes A. |
| EX04 | Branch repair path needs previous step. | Branch and recovery router request targeted rework with loop budget. | Blindly retry current branch step. | Branch test with backward route and manager escalation on budget. |
| EX05 | Required tool is absent from agent capability scope. | Manager grant/reassign diagnostic. | Retry same agent without new capability. | Test proves denied/missing capability is unsafe to retry. |
| EX06 | Tool exists but current run has no required receipt because agent skipped it. | Current-step repair can retry if inputs/access are satisfied and operation is idempotent. | Advance with status-only completion. | Test proves missing receipt blocks completion and retry reason is current-step owned. |
| EX07 | Tool receipt shows policy denial. | Manager access grant/reassignment or terminal policy block. | Retry until attempt budget is exceeded. | Negative test with concrete denied receipt. |
| EX08 | Provider timeout before any artifact output. | Same-step retry only when provider/runtime class is transient and idempotent. | Route to upstream repair. | Test distinguishes timeout from missing artifact. |
| EX09 | Agent returns Completed but managed artifact declares missing blocker. | Finalization rejects completion and routes repair. | Advance because status is Completed. | Finalizer adversarial test. |
| EX10 | Agent writes managed artifact but cites ungrounded paths. | Finalization rejects or requires grounded read/write receipt. | Accept path-like evidence from prompt text. | Existing grounding tests extended to finalization gate. |
| EX11 | Child subprocess is active. | Parent defers until child stops. | Complete parent from pending child. | Subprocess deferred proof. |
| EX12 | Child subprocess stopped blocked with evidence. | Parent propagates concrete child blocker. | Launch duplicate child without manager decision. | Test with stopped child blocker. |
| EX13 | Context package exceeds budget. | Package refs and summaries, require tool retrieval for full content. | Inline every changed product file. | Packaging test with oversized changed-file set. |
| EX14 | Manager confirmation missing after finalization-required step. | Step remains pending handoff or blocked for manager. | Schedule next step. | Runtime state transition test. |
| EX15 | Template declares invalid artifact connection. | Plan/template validation fails before launch. | Let run start and rely on agent prompt. | Builder validation test. |
| EX16 | Unknown failure class. | Manager-required diagnostic with source evidence. | Silent fallback or automatic retry. | Taxonomy default test. |
