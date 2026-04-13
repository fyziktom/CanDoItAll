# Review-gate checkpoints

## Gate A — canonical foundation review

Runs after subbundles 01-03.

Must answer:
- Is there now one canonical dependency representation?
- Is validation pure and normalization explicit?
- Is compatibility isolated enough that later work can trust the model?

Corrective playbook:
- `subbundles/_corrective-foundation-stabilization`

## Gate B — persistence and conflict review

Runs after subbundles 05-06.

Must answer:
- Are save/publish/transition flows conflict-aware?
- Is the save path atomic?
- Do stable child entities retain identity under differential persistence?

Corrective playbook:
- `subbundles/_corrective-persistence-and-concurrency-reset`

## Gate C — publication/runtime/query review

Runs after subbundles 08-10.

Must answer:
- Is publication/version behavior decomposed and race-aware?
- Did runtime extraction produce smaller, testable policy seams?
- Did read-side work actually reduce broad-load assumptions?

Corrective playbook:
- `subbundles/_corrective-runtime-and-query-reset`

## Gate D — consolidation and decomposition review

Runs after subbundles 12-14.

Must answer:
- Were duplicates reduced without creating a shared dumping ground?
- Is the workspace materially easier to reason about?
- Are schema/configuration files and migrations coherent across both providers?

Corrective playbook:
- `subbundles/_corrective-workspace-and-shared-infrastructure-reset`
