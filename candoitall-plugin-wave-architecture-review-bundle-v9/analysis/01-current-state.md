## Current state summary
The codebase is materially better than before. The largest earlier issue — persisted parallel Workbench truth through synchronized system-managed projection rows — looks substantially addressed. The assembly service now builds the current graph in memory and retires old system-managed rows.

At the same time, the deeper closure did not fully happen. Instead, the refactor appears to have shifted several legacy seams into compatibility surfaces that still remain active in production code:
- legacy binding/carrier data was moved into a partial class and a binding table, but both are still active,
- marker normalization now has a JSON representation, but scalar marker fields are still persisted and hydrated,
- plugin manifests exist, but UI/editor flows still only understand the currently known field keys and property bags,
- connector identity is still split between plugin key and legacy enum kinds,
- read paths still perform normalization writes.

This means the architecture is improved, but not yet stable enough for a large connector/plugin expansion.
