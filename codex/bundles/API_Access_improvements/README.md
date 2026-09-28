# CanDoItAll: API access and API-only UI integration

**Implementation handoff for Codex gpt-6 Extra — 20 September 2026**

## Start here

Give Codex `CODEX_PROMPT.md` and this directory. The prompt defines one autonomous engineering task, not a workflow bundle. The supporting documents distinguish observed repository behavior from proposed changes.

Read in this order:

1. `CODEX_PROMPT.md` — execution instructions and non-negotiable boundaries.
2. `docs/01_CURRENT_IMPLEMENTATION.md` and `docs/02_UI_BRANCH_DELTA.md` — pinned source review and selective integration map.
3. `docs/03_ARCHITECTURE.md` and `docs/04_API_AND_CONFIGURATION.md` — target design, compatibility, configuration and HTTP contracts.
4. `docs/05_SETTINGS_UI.md` and `docs/06_VALIDATION.md` — operator experience and executable acceptance criteria.
5. `docs/07_SOURCES_AND_LIMITS.md` — evidence index, external references and review limitations.

`ALL_IN_ONE.md` combines these documents for tools that prefer one input file. It is generated from the individual documents; do not treat both copies as separate requirements.

## Reviewed source snapshot

| Repository/ref | Reviewed commit |
|---|---|
| `fyziktom/CanDoItAll`, `development` | `b82ffc57283f5e4819d82322e1c5bf836dcd9536` |
| `fyziktom/CanDoItAll`, `ui-refactoring-v2` | `e101d5db1478ea329a572db79c0104b927d97f15` |
| Common ancestor | `a2903c400cc35e6d1d2f233c51e73feb256ce2aa` |
| SharedInfo CodeAnalytics guidance | `776a6a329dce5764ba07f6203a8d2ed103d39c2b` |

At this snapshot the UI branch is 35 commits ahead of, and 223 commits behind, development. The latest UI commit is `api spec fixes`. Do not merge or overwrite whole files from that older branch. Reimplement the useful deltas against current development owners and contracts.

## Decision in one paragraph

Keep the current machine JWT capability and trusted local Blazor operator experience. Add small instance-local API accounts, an environment-configured administrative login, and registered user-session JWTs. Keep API exposure, user login exposure, and HTTP access administration separate. Enforce the selected API sections at the endpoints, not merely in the account editor. Do not add registration, global SSR authentication, tenants, a new ownership model, a full identity platform or refresh-token infrastructure to this change.

## What this package is and is not

This is a source-backed architecture and implementation handoff. No CanDoItAll source was changed, no branches were merged, and no CanDoItAll builds, HTTP requests against a running host, or browser tests were executed during preparation. All application validation in the acceptance matrix is **NOT RUN** until Codex implements and executes it.

The optional `tools/capture_review_snapshot.py` creates read-only Git comparison evidence from an existing local checkout. It does not fetch, checkout, commit, merge or run application tests. `examples/*.json` are proposed configuration/contract examples, not files already supported by the reviewed application.
