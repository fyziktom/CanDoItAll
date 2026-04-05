# Retire legacy node carrier
The correct end-state is **not** “the same legacy fields moved to another partial class.” The correct end-state is:
- the node entity no longer owns those fields,
- the main ProjectObjects table no longer stores those fields,
- binding state is stored only in the binding facet / dedicated table,
- read models compose node + binding where needed,
- migration/repair logic runs once, then disappears from hot paths.

Do not close this finding by renaming the file or by keeping the fields as dormant-but-still-active compatibility shims.
