# Original Request

The user reported that Codex completed the previous bundle on branch `maf-processes-refactor` and asked for a review plus the next ZIP bundle. Requirements:

- Do not rush Process Core extraction unless clearly ready.
- Continue smaller dispatcher isolation steps first.
- Keep all original functions and prove no behavior was dropped.
- Prepare abstractions/seams that can later support Process Core and process helper drivers.
- Split into phases/subbundles and enforce refactor gates every few subbundles.
- Provide the bundle as a ZIP.
