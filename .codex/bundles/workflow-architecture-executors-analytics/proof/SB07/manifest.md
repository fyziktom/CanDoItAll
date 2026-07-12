# SB07 Closure Proof Manifest

- Subbundle ID: SB07
- Status: Completed
- Owned requirements: WF-TEST-01 and final integration, architecture, browser, and raw-note closure
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`
- Architecture review: `bundle://reviews/csharp-architecture-gate.md`
- Browser review: `bundle://proof/SB06/browser-validation.md`
- Red-team verifier: `bundle://proof/SB07/closure-verifier.md`
- Final validation and validator transcript: `bundle://proof/SB07/transcripts/closure.txt`

## Closed Evidence

- Final CodeAnalytics snapshot: `snap-20260712222011-fb859aa3`.
- Browser route: `/agents/workflows` (non-artifact local context) at 1600x1000.
- Browser screenshots: `repo://workflow-executors-markdown.png`, `repo://workflow-custom-image-settings.png`, `repo://workflow-plugin-gmail-settings-fixed.png`, and `repo://workflow-analytics-desktop.png`.
- Current-session console errors: 0.
- Browser-found repairs: custom executors bypass the generic creation dialog; the desktop dialog stacking context wins over floating canvas windows.

## Screenshot SHA-256

| File | SHA-256 |
|---|---|
| `repo://workflow-executors-markdown.png` | `503d9f58b8628222dad06cb264770cb769bd9e51a14a152b34f70d2a4cd60e52` |
| `repo://workflow-custom-image-settings.png` | `2b17f634b8044421221311a823f6e45741eda3ef334c2c8698e8773a8ef90a08` |
| `repo://workflow-plugin-gmail-settings-fixed.png` | `b2415a2064a0d1523d9e60295b8033872839180bb95e1136863444855913257b` |
| `repo://workflow-analytics-desktop.png` | `c1cb8cfd3e6c5c1d77d3cb3d969b3f877ec04603cbb4c9f7cc8a6733b6cb09a7` |

## Final Validation

- Solution build: exit 0, 0 warnings, 0 errors, 24.84 seconds.
- Unit: 458/458 final scoped rerun and 526/526 earlier comprehensive scoped run.
- Components: 25/25 WorkflowsPage, 18/18 renderer/catalog (43/43 combined), and 2/2 new custom-renderer focus.
- Integration: 16/16 workflow API, 2/2 email workflow scenarios, and 2/2 real PostgreSQL launch-idempotency.
- EF: no model changes since the last migration.
- Architecture/browser: final snapshot and 1600x1000 proof passed with 0 current-navigation console errors.
- Completed-stage validator: exit 0; `bundle://proof/SB07/transcripts/closure.txt`.
