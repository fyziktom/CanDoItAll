# Reproducible edit-to-visible protocol

Freeze before starting: OS/machine/CPU/memory, .NET SDK and runtime, Node/Tailwind lockfile, browser/version, 1600×1000 viewport, repo HEAD plus working-tree source hashes, sibling paths/revisions, environment variables affecting watch, project references and static-asset hashes. Use task-owned isolated full-app data and the same catalog fixture semantics in both hosts. Do not measure against personal runtime data.

Use managed SourceWatch sessions or equivalent verified dotnet watch run --project <exact project> --configuration Debug commands. Record the actual command, launch directory, runtime PID/revision and readiness. Preserve live Components/FileTools mode. Only one app measurement host and its necessary Tailwind watcher run at a time. Stop only task-owned processes. Restore package/assets once outside warm samples and record cache state.

Cold startup is a separate ledger:
- Process-cold startup with restored/build caches present: command start to first complete, interactive, asset-ready catalog; at least three starts per host.
- If a clean-compilation cold figure is wanted, collect it separately in isolated outputs/checkouts and label it; never mix it with process-cold or warm trials.
- Separate backend/database boot time from rendering readiness as observable milestones, while reporting the full wall-clock startup too.

Warm protocol has three distinct reversible edits in each category, each with at least three trials per host (minimum 54 primary comparison samples, plus the pre-extraction full-app baseline). Freeze exact source paths/diffs and visible assertions before trials:
- Razor: visible catalog heading text; visible empty-state wording in its fixture; visible action label in a representative card state. Change existing markup/text only.
- C#: existing pure presentation method return text, metadata formatting text, and team title text. Change supported method bodies, not public signatures, inheritance, added state fields or generic type shape.
- CSS: existing scoped catalog toolbar gap, existing card/layout spacing, and an existing Tailwind-input semantic style declaration. Assert computed style, not file timestamp. Keep the real Tailwind watcher active for the third case.

For every trial: confirm idle/ready watch state; navigate to the frozen fixture/state; flush the exact edit; observe the real DOM/text/computed-style predicate after rendering; capture watch/browser evidence; revert the patch and await its visible undo plus ready state before the next trial. Do not count undo as an extra successful trial or silently retry a slow/failing edit. Keep the same equivalent edits before/after relocation; record path moves.

Timing: one monotonic coordinator clock records t0 immediately after the edited file is flushed and t1 when the browser assertion confirms the visible update (prefer two animation frames after the predicate). This includes observation polling/transport; record interval and overhead consistently for both hosts. Do not subtract server wall time from browser performance.now without clock alignment. Watch/compile timestamps are diagnostic milestones, not substitutes for visible completion.

Required warm ledger columns: trial ID, host, source/SDK/asset manifest IDs, edit category and exact patch hash, ready revision/PID, monotonic t0/t1, edit-to-visible milliseconds, browser assertion, hot-reload generation/watch event, actual browser reload, PID/revision change, mechanism, outcome, artifact links and undo confirmation.

Classify every attempt as hot reload, browser reload, process restart, or failure, using watch logs plus browser navigation and process identity. Record mixed mechanisms explicitly. Unsupported/rude edits stay visible as failures/restarts, not removed from summaries or substituted to improve results.

Report per host/category/edit and aggregate category minimum, maximum, range and median, sample count and failures. Report mechanisms separately; a restart is not hot reload. Provide raw compact CSV/JSON and a runnable measurement recipe using actual local paths/configuration. Compare full app before extraction, full app after extraction and sandbox with matched fixtures/asset semantics.

A valid experiment can show no improvement. No percentage/performance claim without these comparable measurements; no expected numeric speedup or arbitrary success threshold. If noise or assets/runtime mismatch invalidate comparability, retain raw trials, explain the invalidation and do not claim an improvement.
