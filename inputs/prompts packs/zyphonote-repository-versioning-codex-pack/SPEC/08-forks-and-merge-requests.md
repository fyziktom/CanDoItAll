# Forks and merge requests

## Why forks matter in Zyphonote

Forks are useful for:
- arranging a public score into a personal variation,
- creating a lesson/package variation from a template,
- proposing curated playlist changes,
- internal same-owner experimentation before merge.

## Fork policy recommendation

Add a per-repository fork policy:

- `disabled`
- `same_owner_only`
- `public`

### Defaults
- score: `same_owner_only` unless explicitly public-forkable
- learning_package: `same_owner_only` unless explicitly public-forkable
- playlist: `same_owner_only`
- event: `same_owner_only`

## Rights / marketplace rule

A fork of someone else’s sellable content must **not** automatically become sellable.

Suggested rule:
- forked repos default to:
  - private visibility,
  - listing creation blocked,
  - derivative-rights review required before publication/listing.

This is important for copyrighted music and commercial lesson content.

## Fork creation flow

1. user chooses source repo + source branch
2. system validates fork policy + read permission
3. new repository is created
4. new repository stores `upstream_repository_id`
5. chosen source tip becomes the new fork default branch tip
6. branch/ref metadata records the upstream origin

## Merge request model

A merge request is a proposal from:
- source repo + source branch
to
- target repo + target branch

### Required fields
- title
- description
- source repo
- source branch
- source head commit hash
- target repo
- target branch
- target head commit hash
- merge base commit hash
- status
- mergeable state
- merge strategy

## Merge strategies for v1

Support:
- `merge_commit`
- `fast_forward`

Do not require:
- squash
- rebase

These can be added later.

## Mergeability states

Suggested enum:
- `unknown`
- `clean`
- `conflicts`
- `behind`
- `blocked`
- `merged`

## Required checks before merge

- MR is open
- source and target branches still exist
- target protected-branch rules allow merge
- source head still matches MR head
- target head still matches or MR is re-evaluated
- merge preview is still clean
- permissions allow merge

## UI expectations

### PHP phase
At minimum provide:
- create fork action
- list own forks
- create MR form
- MR detail view
- mergeability status
- merge action for clean MR
- compare preview summary

### WASM phase
The client can later:
- open compare view
- fetch conflict hunks
- resolve and submit merge proposals

## MR comments / review threads

Optional for v1.
Design should leave room for them later, but they do not need to block the first implementation.

## Audit requirements

Write audit entries for:
- fork created
- MR opened
- MR closed
- MR merged
- protected-branch merge rejected
- rights/policy rejection on fork publication/listing
