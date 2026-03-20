# Testing, seed data, and rollout

## Test strategy

Use three layers:

### 1. Unit tests
Prefer adding PHPUnit in dev/CI even if shared hosting does not run it.

Test:
- canonical JSON hashing
- blob dedupe
- snapshot hashing
- commit hashing
- branch CAS update
- fast-forward detection
- merge base resolution
- fork policy decisions
- score merge hunk generation
- playlist/package/event structured merge

### 2. API smoke tests
Extend shell smoke coverage for:
- create repo-backed entity
- commit
- create branch
- compare
- merge-preview
- merge clean branch
- fork
- create MR
- merge MR

### 3. UI/manual regression
Verify:
- score detail page graph
- playlist page graph
- event page graph
- learning builder graph
- existing marketplace/planning features still work

## Suggested new test files

- `tests/RepositoryHashingTest.php`
- `tests/RepositoryBranchingTest.php`
- `tests/RepositoryMergeRequestTest.php`
- `tests/ScoreMergeServiceTest.php`
- `tests/PlaylistMergeServiceTest.php`

And shell smoke:
- `TESTS/api_v1_repository_smoke.sh`

## Seed data requirements

Extend `tools/seed_dev_data.php` with repository scenarios.

### Score scenarios
- linear history on `main`
- feature branch with 2 commits
- clean merge into `main`
- conflicting branch pair for compare testing

### Playlist scenarios
- rehearsal branch
- client-request branch
- one shared frozen commit

### Event scenarios
- logistics branch
- rain-plan branch

### Learning package scenarios
- published branch/tag
- draft feature branch
- one forked package draft

### Fork / MR scenarios
Create at least:
- one upstream score repo
- one fork repo
- one open MR
- one merged MR
- one blocked MR due to conflicts

## Rollout order

### Stage A
- enable repository backend
- enable backfill
- enable graph UI read-only

### Stage B
- enable branch/commit actions for internal users
- keep forks/MRs behind feature flag if needed

### Stage C
- enable WASM sync endpoints
- enable fork/MR UI broadly

## Success criteria

- no regression in score create/edit/purchase flows
- no regression in package authoring
- no regression in playlist/event planning
- graph visible and understandable on all four root entity pages
- exact commit pinning visible in inspector/API
