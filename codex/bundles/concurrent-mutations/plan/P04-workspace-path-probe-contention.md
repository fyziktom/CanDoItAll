# Remove filesystem case probes from exact workspace paths

Status: implemented after signed `e4be2d239`; [75 selected cases pass](../validation/2026-10-20-workspace-path-probe.md).

The enlisted task publication run retained a failed responsiveness reference: two of six
unrelated writers exceeded one second. They were not blocked by the five-second SQL wait;
workspace coordination, file publication and preparation consumed the time. The source
audit found that every ordinary path-policy factory construction probes case sensitivity
by creating/deleting a temporary file. Durable publication repeatedly reconstructs that
policy while the workspace gate is held. This is unnecessary I/O for engine-generated
paths and is a concrete optimization candidate, not yet a measured improvement.

Allow the existing Infrastructure path-policy factory to have an explicit strongly typed
comparison mode. Preserve the parameterless detected-mode behavior for all existing users
and the explicit per-call override. Reject Unknown and undefined configured modes.
Configure only the file workspace store's existing factory as Sensitive. The storage
layout builds exact path spellings from its supplied absolute root, fixed directory/file
names, GUIDs and encoded filenames. Ordinal containment is conservative on every host;
it does not infer or cache filesystem case sensitivity. Aliased spellings outside the
exact root are not authorized. Existing file paths and protocol bytes do not change.

Every policy is still constructed fresh and checks ancestors from the native root.
Keep all containment, link/reparse, parent existence and before/after-wait/commit checks,
durable flushes, exclusive OS locks, journal stages and rollback/recovery semantics.
No long-lived filesystem safety cache, removed security check, lock weakening, new module,
dependency or registry is permitted. Existing injected test writers keep their own explicit
factory. Retained historical readers are a separate owner and are not widened by this change.

Prove configured factory validation, exact-root containment, missing-root handling and
link replacement through real filesystem operations. Repeat durable publication,
workspace prepared-commit conflict/canonicalization and journal recovery tests. Repeat
the identical opt-in selected-authority SQL-wait reference, retaining all observations
and failures; keep its one-second requirement. An improved small cohort does not qualify
maximum payload, sustained load, p95 or fairness. If it fails, inspect the trace and keep
performance open; do not rerun unchanged code until green. Build Infrastructure and
Persistence plus affected tests, run full portability enforcement and architecture review,
freeze observed execution, reconcile resources, update documentation and sign the checkpoint.
