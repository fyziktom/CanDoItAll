# Desktop-only and useful development-loop evidence

Primary viewport: 1920x1080, 100% zoom. Optional 1600x1000 only for a concrete large-desktop issue.
No new mobile/tablet/small/medium layout tuning. Keep existing tests; do not delete them to hide
failure, but do not expand this task into responsive redesign. Required checks: actual field input,
keyboard, focus, modal stacking, table scroll, complete footer, nested dialog ownership, menus,
fonts/scoped CSS and browser/circuit errors. All child renderers must be visible and interactable;
DOM presence alone is not geometry proof.

PP1 reported repeated sandbox hot reload stalling on edit two; the successful samples used
rebuild/restart with hot reload disabled. This is a distinct tooling limitation, not confirmed
proof of provider data failure. Preserve PP1 historical results and first failures (R01).

For PP2 record unchanged baseline then final evaluated project graph, restore assets/reference
mode, watch list, SDK, source/dependency SHAs, host configuration, first/warm build, launch, hydration
and edit-to-visible samples separately. Measure three consecutive reversible edits for Razor, C#
and scoped CSS, and feature-owned JS if actually present. Save hash before/after and restore it.
No expensive test/build may run concurrently against the same outputs.

Use a clean isolated owned checkout/copy or output configuration that honors actual sibling
source references; do not switch the user's working branch, keep untracked product inputs visible
in provenance, and preserve historical packages. Distinguish polling/watching, hot reload and
rebuild/restart. Identify whether failure is detection, compilation, hot-reload application,
served asset version or browser rendering. Bound the investigation; do not loop blind retries.

A useful new observation is not a universal speedup claim. If repeated hot reload still fails,
keep a working isolated rebuild/restart loop and report the exact reproduction plus task-limited
workaround. Do not globally disable hot reload, suppress file watching, exclude relevant inputs,
change SDK/package versions or relax checks just to get a faster number. No follow-up latency
number should mix different methods without a label.
