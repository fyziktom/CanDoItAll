# Real source and client validation

## Status

- Completed
- Build6 source and client pass all three real UI tests, exact full catalog
  and price/private parity, both providers' chats/agents, approved image completion and vision.
- Source ledger: eight successful complete invocations and one image. Fresh generated PNG
  inspected; both hosts HTTP 200 Healthy with zero new failure log headings.
- Final verifier and handoff limits: reviews/02-final-verifier.md and reviews/01-execution-report.md.

## Objective

Hand off real, healthy source/client Docker apps with matching full model inventories.

## Covered Inputs

- N001-N004 / R1-R4 live acceptance and original-input closure.

## Prerequisites

- SB01 foundation passes. Real OpenAI credential and reachable operator Ollama.
- Ollama failure blocks its proof; never substitute a fixture.

## Exact Source References

- repo://codex/bundles/shared-providers/subbundles/SPMETA-source-metadata-mirroring/proof/Restart-TestInstances.ps1
- repo://codex/bundles/shared-providers/subbundles/SPMETA-source-metadata-mirroring/proof/Run-TwoInstanceUi.ps1
- bundle://requirements/01-normalized-requirements.md

## Deliverables

- Rebuilt 5210/5212 apps preserving database/volumes and recoverable old deployment.
- UI-only source provider setup against actual endpoints; UI client synchronization.
- Exact full catalogs, honest rates/private metadata, real calls and source usage proof.
- Explicit blocker instead of fabricated completion if Ollama remains offline.

## Dependency Impact

- Final user handoff depends on real positive evidence and inspected UI; fixture unit
  or integration tests alone do not lend this trust.

## Validation Depth

- Proof tier: Governed.
- Test/check: source/client real browser UI and upstream HTTP inventory read-only checks.
- Topic: provider identity, full catalog/pricing mirror, nondefault selection/execution,
  chat, agent, image generation/analysis, centralized usage.
- Expected named cases: source Ollama inventory; source OpenAI inventory; full client
  equality; real nondefault chat; agent; image; vision; source usage; failed refresh.
- Invalidation keys: deployment image/config, provider catalog, runtime selection or routing.
- Broad-gate decision: Not required. Browser/host checks own this acceptance.

## Implementation Steps

1. Rebuild and restart exactly the two test app containers, retain rollback and volumes.
2. Set real endpoints/secrets/models through UI and refresh/save source catalog.
3. Synchronize source through client UI; compare full names, prices and private flag.
4. Use nondefault real models in chats/agents and image/vision; inspect source usage.
5. Inspect screenshots/health, record hashes/transcripts and close notes honestly.

## Acceptance Checklist

- No synthetic fixture endpoint/default/model/rate remains in handed-off shared profiles.
- Source inventory equals configured real upstream; client equals source.
- Successful real executions, UI model selection, source usage and healthy app responses.
- No credentials exposed and no changes to 5032.

## Proof Required

- proof/SB02/manifest.md and semantic-invariants.md with browser/host transcripts,
  inspected screenshots, image digest and sanitized configuration/parity assertions.
- Compare identities/values, not just counts; preserve UI setup evidence.

## Browser Validation Logging

Record 5210 and 5212 providers, agent Runtime and chat/image pages at 1920x1080.
Capture normal Prices views and open agent model dropdown. Inspect clipping, primary actions,
readability and existing editor/dialog/table scroll ownership.

## Progression Gate

- Required real cases pass or exact external blocker is recorded; no full completion claim
  while any required upstream/execution proof is missing.

## UI Composition Contract

Keep existing desktop split list/editor with Connection, Prices, Runtime and Sharing tabs.
Primary content is editable connection/catalog or readonly mirrored metadata. Counts remain
compact badges, not a dashboard. No new textareas/dialog sizes. At 1920x1080 identity and
primary actions remain discoverable; editor/dialog owns vertical scroll, table horizontal
scroll. Inspect normal pricing and agent model dropdown/overlay states.

## Do Not Do

No alias abstraction, fixture-only live proof, model-name blacklist, new project references,
unrelated cleanup, 5032 mutation, secret logging or full-suite run.

## Reopen Triggers

Stale rows after refresh/save/sync or mismatched nondefault names reopen SB01 and SB02.
Fixture endpoint or missing real execution/usage reopens SB02.
