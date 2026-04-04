# Original Request

## Current Request

- User date context: `2026-04-04`
- User request:
  - `use the [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) improve bundle with all new findings and then execute bundle and validate results.`
  - `we must have those things correctly otherwise we will have large trouble in future. At the end analyze it again with those new skills to olve possible canonical troubles with models.`

## Prior Context That This Bundle Must Preserve

- The older `v2` architecture-review bundle was already executed once but is stale against the current bundle-validator contract.
- A follow-up architecture review found unresolved canonical-model risks around:
  - dual-written node-local party ownership
  - missing assignment cleanup on node delete
  - missing assignment transfer on subtree move
  - raw-string `NodeKey` bridge weakness
  - missing lifecycle tests for canonical node assignments
- The repo now includes the integrated architecture-review skills and agents that must be used for the post-fix analysis pass.
