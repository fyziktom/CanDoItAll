# Full-catalog governed proof

State: DONE, completed-stage gate passed. This supersedes the old metadata-only
closure for the newest screenshot. Historical SB07 remains blocked.

## Contract and provenance

Owned invariants: FULL-SET, FULL-ISOLATION, FULL-UI, FULL-RUN, FULL-HANDOFF; exact meanings,
input, source inventory, boundary plan and re-entry triggers are in full-catalog-repair.md.
Baseline HEAD: 0ecb6307823576e80f79074187668771b166609a (initially clean).
No commit, staging, discard or skill edits. Original proof remains historical.
full-catalog-changed-files.json records raw baseline-blob and actual worktree SHA-256
for every changed source/test/bundle/proof file. Self-hash exceptions are explicit there.

## Focused test evidence

Each named transcript includes cwd, command, discovery, run output and exit status; its
matching TRX is machine checked by Validate-FullCatalog.ps1. No whole-suite claim.

| Final lane | Discovered/passed | Transcript stem |
|---|---:|---|
| Publication/source projection/snapshot/Simple Chats resolver | 52/52 | full-catalog-unit-2 |
| PostgreSQL/HTTP catalog, reconciliation and runtime projection | 52/52 | full-catalog-integration |
| Provider/model/pricing/agent/Simple Chat components | 24/24 | full-catalog-components |
| Agent save, provisioning, workspace and package-import consumers | 39/39 | full-catalog-agent-save-2 |
| Isolated authenticated Simple Chats UI | 1/1 | full-catalog-simple-chats-probe-2 |
| Complete two-instance UI | 1/1 | full-catalog-ui-6 |
| Complete two-instance UI repeat | 1/1 | full-catalog-ui-repeat |

Total: 167 focused final non-browser test executions. Initial FULL-SET regression failed
in full-catalog-red; four source-model save cases failed in full-catalog-unpriced-red-2.
Both are real behavioral failures before production repair, not build failures.

CodeAnalytics impact result: full-catalog-impact.json, incomplete/Low confidence; manual
boundary selection is justified in the plan. Fresh production boundary scan passes in
full-catalog-boundary.json and transcripts/full-catalog-architecture.txt. Source assertions,
anti-stub inspection, exact changed-file hashes and SB07 preservation are completed-stage
checks in Validate-FullCatalog.ps1, not substitutes for behavior tests.

## Production artifact matrix

| Artifact | Producer/consumer | Lifecycle and negative proof |
|---|---|---|
| Effective model catalog | Shared policy used by source runtime mapper and strict publisher; actual HTTP import/runtime projection | UI source additions/resync; default-first; distinct opaque routes; Ollama/image isolation; expanded limit rejects |
| Model labels and prices | Source normalized prices and catalog; Simple Chats runtime/Application/presentation records | Independent source UI and agent-selector oracle; exact nine-field prices/private state; missing prices remain unavailable |
| Saved agent/chat selection | Real UI save boundaries and published-model policy | Save/reopen non-default selection; reject unpublished/priced and missing constraints; local manual-price rule retained |
| Execution and central usage | Real source relay and client runtime with deterministic upstream | Non-default agents/chats; image approval/resume; attached-image input; production ledger and safe upstream captures, never injected success rows |

Both complete browser runs and their independent runtime checks pass:
transcripts/full-catalog-ui-6-runtime.txt and full-catalog-ui-repeat-runtime.txt.
Each run has ten central Succeeded operations with Complete usage, including one generated
image, and all four selected non-default models. Both engines return healthy HTTP 200
and have no error/critical/unhandled-exception headings during either run. A fresh PNG
with a valid signature and attached-image upstream input were independently verified.
Metadata JSON and desktop screenshots are in browser/<run-label>/. Their independent
source/client sets contain 12 OpenAI chat and 3 Ollama choices; prices/private match.
Non-default agent and Simple Chat choices, image generation and image analysis all execute.
Price metadata parity does not establish billed cost.
The deterministic fixture tests request routing, persistence and UI behavior, not vendor
model availability, paid OpenAI calls, or the visual quality of generated images.

## Build and handoff identity

Release image: candoitall-shared-providers-ui:fullcatalog-20260827-2,
sha256:db76a05c23434b3fb660c9d4546c9dcd725add605ce76382441939f85d914d6d.
Build: transcripts/full-catalog-docker-build-2.txt. Replacement/rollback:
transcripts/full-catalog-restart-2.txt. Source 5210, client 5212, upstream fixture 5213;
named volumes retained, 5032 and unrelated PostgreSQL untouched.
Final test-project build: transcripts/full-catalog-ui-build-final.txt (zero warnings/errors).
Installed Chrome 151.0.7922.174 is required by this environment's acceptance lane.
Browser auth uses a UI-issued client JWT with only Simple Chat scopes and blocks other
HTTP origins. Safe auth facts are in browser/<run-label>/simple-chat-auth.json.

Primary-agent architecture/semantic/desktop review: reviews/full-catalog-review.md.
No independent-review claim. Final operator handoff is RESULT.md.
Completed-stage command: proof/Validate-FullCatalog.ps1; transcript:
proof/transcripts/full-catalog-closure.txt, exit 0. All FULL-* invariants satisfied;
273 indexed changed artifacts verified, self-hash exclusions explicitly documented.
