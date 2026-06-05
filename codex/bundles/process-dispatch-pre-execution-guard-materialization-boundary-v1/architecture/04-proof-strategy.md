# Proof Strategy

Required proof:

1. Full solution build.
2. Processes module build after critical moves.
3. Focused unit/architecture tests for helper boundaries.
4. Focused integration tests for:
   - no missing upstream artifacts,
   - missing artifacts with no runnable target,
   - missing artifacts with runnable target,
   - duplicate materialization fingerprint,
   - downstream block transition request fields,
   - upstream rerun request directive.
5. Source scans:
   - no Process Core,
   - no driver API,
   - no UI files,
   - no prohibited proof paths,
   - no stubs,
   - no direct fingerprint/journal/rerun construction left inline except wrappers.
6. Line-count and hotspot review.
