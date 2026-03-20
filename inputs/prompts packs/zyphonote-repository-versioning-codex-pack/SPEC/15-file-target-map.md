# File target map

## New shared libs
- `src/lib/repositories.php`
- `src/lib/repository-hashing.php`
- `src/lib/repository-score.php`
- `src/lib/repository-planning.php`
- `src/lib/repository-learning.php`
- `src/lib/repository-events.php`
- `src/lib/repository-api.php`
- `src/lib/repository-ui.php`

## Existing files to update
- `src/api/v1/index.php`
- `src/lib/config.example.php`
- `src/lib/config.php`
- `src/lib/user-auth.php`
- `src/lib/planning.php`
- `src/lib/learning-packages.php`
- `src/account-score-detail.php`
- `src/account-dashboard.php`
- `src/account-playlists.php`
- `src/account-events.php`
- `src/account-learning-builder.php`
- `tools/seed_dev_data.php`
- `TESTS/api_v1_smoke.sh`

## New UI assets
- `src/assets/js/zy-repository-graph-canvas.js`
- `src/assets/js/zy-repository-graph-page.js`
- optional CSS additions in `src/assets/css/app.css`

## New tools/tests
- `tools/repo-backfill.php`
- `tools/repo-verify.php`
- `tools/repo-gc.php`
- `tests/RepositoryHashingTest.php`
- `tests/RepositoryBranchingTest.php`
- `tests/ScoreMergeServiceTest.php`
- `TESTS/api_v1_repository_smoke.sh`
