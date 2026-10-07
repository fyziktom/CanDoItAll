# Source pair and delivery

Reviewed main `9969913fe653500a59d24af06482f48b758a5ca6` and published Components `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1` are provenance, not pins.
The AC1 report used FileTools `3a080ecd31068a77c1e1bd639f7a78e21c93db85` and records the existing
CI pin `498b36825bd5a5222429972af120b04becf4b3f6` as the same tree. Recheck actual refs/tree equality;
do not gratuitously repoint that dependency or repeat a historical publication blocker.

At entry read current Directory.Build.targets/package props/CI and evaluate source-resolution
rather than infer it from package versions. Capture protected existing UI/sandbox roots,
including complete Agents, Workflow shell/authoring, providers, Workspace and Projects.
At final compare exact path sets and forbidden edges. A type forwarded for compatibility
must not accidentally load a backend into the new leaf's closure.

Record each native image ID, actual base labels, all dirty source input hashes and the
matching DLL/static-asset bytes. Never relabel an older image as a new commit. Updating apps
within owned multi-instance fixtures must preserve their DB/upstream identities when proving
restart/state continuity. Tests and documentation committed later retain their own source
relation; do not report the earlier image as a newly rebuilt final image without comparison.

A new local-only Components change can be tested, but final delivery must state local versus
remote availability separately. Do not add a release/push to this task without explicit user
authorization. Missing external infrastructure is a blocked test, not a passing product lane.
