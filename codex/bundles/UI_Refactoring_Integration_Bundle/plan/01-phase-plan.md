# Phase Plan

## Phase 0 — Freeze and guard

Run SB01. Capture branch tips, worktree state, remote URLs, SDKs, and v2 denylist. No functional
changes.

## Phase 1 — Repair Components upstream

Run SB02 from current Components main on an integration branch:

- reproduce three failures,
- review and update governed baselines,
- repair BaseLib output CSS source contract,
- run full CI-equivalent validation.

Stop if Components cannot become green without unrelated changes.

## Phase 2 — Normalize versions

Run SB03:

- query feeds,
- select `V`,
- apply central versions,
- remove FileTools project overrides,
- build package families locally.

Do not publish.

## Phase 3 — Validate FileTools

Run SB04:

- dependency audits,
- full tests/format/package validation,
- standalone sandbox proof.

Expected source changes beyond version metadata are zero.

## Phase 4 — Merge development into original branch

Run SB05 in CanDoItAll:

- merge current development with a merge commit,
- resolve the five known branch deltas according to policy,
- run scope guard and compile-level smoke.

## Phase 5 — Adapt main application

Run SB06:

- static asset host change,
- raw icon DOM migration,
- CSS selector migration,
- test selector migration,
- minimal compile fixes for current Components API.

Do not import v2 UI.

## Phase 6 — Pins and operations docs

Run SB07:

- update source SHAs,
- assert sibling static assets,
- modernize Podman/macOS documentation,
- prepare local package-feed validation.

## Phase 7 — Cross-repository proof

Run SB08 in staged order. Keep targeted and broad results separate. Capture screenshots/logs
under the bundle proof path or repository-owned ignored artifacts.

## Phase 8 — Merge closure

Run SB09. Re-run v2 guard, produce ancestry/version/proof summary, and leave explicit owner merge
instructions. Perform remote writes only when authorized.

## Restart strategy

Each subbundle has a completion marker in the execution report. On restart:

1. refresh branches,
2. verify prior result SHAs still exist,
3. rerun the subbundle's progression gate,
4. continue only if the gate remains green.

If an upstream branch moves, reopen the earliest subbundle whose assumptions changed.
