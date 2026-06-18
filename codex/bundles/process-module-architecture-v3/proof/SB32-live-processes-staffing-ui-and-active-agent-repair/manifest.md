# SB32 Proof Manifest

## Scope

Repaired the follow-up defects reported on 2026-06-17:

- Software-delivery parent subprocess staffing no longer makes Delivery Manager the visible owner for .NET architecture and implementation work.
- Project-structure process start closes the HR review dialog and carries a route-visible started notification into Live Processes.
- Live Processes one-hour window excludes stale active runs.
- Live Processes shows first-tab activity cards, active/stale agent cards, detail dialog context, and full-width desktop tabs.
- The stale Tetris run is now visible as stale expired work instead of being presented as actively working agents.

## Root Cause

The matching resolver already had role/capability scoring, but the parent software-delivery template was giving technical subprocess steps to `delivery-manager` as `Responsible`. The launch plan then faithfully resolved Delivery Manager for `.NET architecture` and `.NET implementation` because the template told it to. This was a template ownership bug, not only a scoring bug.

The Live Processes projection also treated active runs specially and allowed old active runs through the selected time window. That made the one-hour view show stale work. Active-agent visibility was missing because the runtime workspace projection did not join runtime state to step assignments.

## Proof Files

- `changed-file-hashes.txt`
- `semantic-invariants.md`
- `validation.md`
- `ui-ux-parity-analysis.md`
- `live-window-after.json`
- `output-folder-check.json`
- `browser-liveprocesses-initial-snapshot.md`
- `browser-liveprocesses-agents-snapshot.md`
- `browser-liveprocesses-detail-dialog-snapshot.md`
- `browser-liveprocesses-mobile-snapshot.md`
- `browser-liveprocesses-agents.png`
- `browser-liveprocesses-detail-dialog.png`
- `browser-liveprocesses-mobile.png`
- `transcripts/unit-tests.txt`
- `transcripts/component-tests.txt`
- `transcripts/solution-build.txt`
- `transcripts/git-diff-check.txt`

## Runtime Result

The repaired UI/API show the old broken Tetris launch as two recent active runs with expired leases and zero working agents. `C:\programovani\dotnet\output` still contains zero items, so the existing launch did not finish the final app. The fix prevents the stale state from being hidden and prevents new technical subprocess launches from assigning Delivery Manager as the default technical owner.
