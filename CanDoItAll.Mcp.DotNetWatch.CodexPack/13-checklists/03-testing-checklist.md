# Testing checklist

## Unit tests
- [ ] Options validation
- [ ] PathGuard
- [ ] Env whitelist filtering
- [ ] LogRedactor
- [ ] Session compatibility comparer
- [ ] Wait condition evaluator
- [ ] Diagnostic category matcher

## Integration tests — P0
- [ ] stdio cleanliness
- [ ] workspace_info
- [ ] WatchRun start
- [ ] RunOnce start
- [ ] app_stop kills tree
- [ ] app_logs cursoring
- [ ] app_wait Healthy
- [ ] app_wait QuietSinceCursor
- [ ] solution_build StopAndResume
- [ ] tests_run StopAndResume
- [ ] unexpected exit detection
- [ ] stale cleanup
- [ ] path outside workspace blocked

## Integration tests — P1/P2
- [ ] port conflict diagnostics
- [ ] health timeout diagnostics
- [ ] launch browser suppressed
- [ ] watch exclusions
- [ ] runner detection summary
- [ ] cross-platform process tree nuances

## Manual test passes
- [ ] local Windows
- [ ] local Linux or WSL
- [ ] optional macOS
- [ ] slow rebuild scenario
- [ ] rude edit scenario
- [ ] invalid config scenario

## Evidence
- [ ] Validation matrix je aktualizovaná dle reality.
- [ ] Selhání testů mají uložené logy/artefakty.
- [ ] P0 scénáře jsou zelené před merge.
