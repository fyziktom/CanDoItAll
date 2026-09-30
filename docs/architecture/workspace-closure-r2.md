# Workspace closure R2

Workspace rendering remains complete. Final composed validation is in progress;
`ready_for_next_module=false`. No new module is authorized by this repair campaign.
The [module map](modules.md) now records the completed leaves and a provisional remaining
roadmap. Its estimates are planning ranges, not a complete audit of every remaining renderer.

## Repairs and validation boundaries

- [WCL-DEL1](workspace-agent-deletion-closure-finding.md): the original retained file bytes
  reproduce strict receipt-count underflow before journal admission. The independent audit
  writer can persist receipts and artifacts without updating the run-detail counter delta.
  Deletion reconciles only those derived counters and the existing session count. Target
  ownership, pending approvals, unresolved effects and other corruption checks remain strict.
- [WCL-NAV1](workspace-navigation-acknowledgement-finding.md): real framework JS dispatch
  controls distinguish a visible route from the initiating navigation's acknowledgement.
  The usage journey waits for its actual acknowledgement and server completion before
  teardown. Active timeout remains observable; permanent retirement uses the framework's
  session-ended path. No product navigation manager, timeout, retention or error allowlist
  changed. The historical caller was not instrumented, so its exact correlation is not
  retrospectively claimed.
- The first R2 broad Unit attempt exposed a separate test-fixture inconsistency. The
  non-cooperating strategy's factory still honored cancellation before execution started.
  A controlled delayed-creation row reproduces the failed startup observation. The factory
  now consistently simulates non-cooperation, and cleanup covers failed observations too.
  All 39 owning-class cases pass. Production Process timeout behavior is unchanged.
- The second broad browser attempt exposed an input-admission gap in the storage selection
  test. Holding the real Dialog open invocation leaves its option attached but invisible;
  `FocusAsync` and `PressAsync` can then complete without selecting it. The test now waits
  for visibility, verifies the empty draft and observes focus before sending Space. The
  controlled delay proves this harness gap; the original failure lacked the observations
  needed to attribute its precise timing. The isolated unchanged test passed, which does
  not erase the broad failure. A new frozen campaign follows this correction.

The first broad Stable/browser attempts were interrupted for that fixture correction and
an evidence hygiene defect. A historical general JSON receipt contained the secret scanner's
synthetic key parameter. Its exact original is preserved with DPAPI; only that known test
parameter was redacted, with before/after hashes. No historical test outcome or exhausted
live-budget entry was rewritten. The interrupted attempts do not establish closure.

## Source and dependency delivery

The entry app was `6b05246f72fa29ad069a85d12881c80631de263f`. Signed production repairs are
in `56088be3f`; later corrections affect Unit and browser test observation only. Raw private proof
is under `artifacts/workspace-closure-r2/20260930-6b05246f7`, outside the sealed package.
Each attempt records its own source manifest and exact discovery/execution result.

The actual local Components checkout is signed merge
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`, with the same tree
`1e318a37f187c120e74d88357715ba22ae5cec31` as repaired commit
`22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e`. The rebuilt consumer assembly identifies `4a858412`.
An isolated clean clone discovers 437 entries, expanding to 443 passing cases: two form
theories expand into five and three rows. The private BaseLib package records the actual
merge commit and contains identical Dialog and initializer JS bytes to the source checkout.

Local FileTools `3a080ecd31068a77c1e1bd639f7a78e21c93db85` and CI pin
`498b36825bd5a5222429972af120b04becf4b3f6` share tree
`6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`. Both revisions build all eight application-consumed
libraries. Evaluated framework, language, compilation, package and reference settings agree
after normalizing the two clone paths. Informational versions include their different Git
revisions, and binary hashes are recorded separately; binary equality is not claimed.

Remote delivery remains a separate gate. CI selects Components using
`github.base_ref || github.ref_name`, then pins the resolved SHA for build jobs. The observed
remote development branch still points to `ff5289746573a6dd6456754a7764844627ddd49f`;
it is an ancestor of the tested local merge. No remote write is authorized in this campaign.
After explicit publication authorization and a fresh remote check, the operator must deliver
the tested Components commit to the application's actual target branch before running its
CI. For a development-targeted app PR, the prepared normal fast-forward is:

```powershell
git -C ../CanDoItAll.Components push origin 4a858412d2c2a3f6123bf23d8c4584f05b47627d:refs/heads/development
```

That command has not been executed. A main-targeted consumer requires its own compatible
Components integration. A local package or application push cannot establish remote delivery.

## Required final decision

The fresh composed campaign must retain all 38 closure groups, original UI deletion and
projection read-back, real scripted two-turn history cleanup, current full Stable and browser
dispositions, served assets, protected graphs, headless restarts and final secret review.
The first eight evaluated graph/watch closures match the previous final source pair exactly.
Portability enforcement passed with 15,214 unchanged reviewed findings; it will be repeated
after the final test-fixture edit. All 53 package files and 52 tooling controls pass.

The previous live journal remains exhausted at 40/40. This bundle authorizes zero new model
requests. Missing live authority, external fixtures, macOS execution and remote delivery must
remain explicit prerequisites; they cannot be inferred from local deterministic results.
Product-fix closure and complete application readiness are separate decisions.
