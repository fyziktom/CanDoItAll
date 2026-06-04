# Dispatcher Slice Map

Known dispatch responsibilities to isolate over multiple future bundles:

1. Execution start/detail/list/failure boundary — this bundle.
2. Execution receipt and required-tool observation — this bundle starts the helper boundary.
3. Artifact projection and artifact status validation — future bundle.
4. Project-structure grounding and external target grounding — future bundle.
5. Browser proof and hosted browser evidence — future bundle.
6. DotNet/web-host cleanup and domain-specific SW dev heuristics — future driver-prep bundle, not this one.
7. Recovery packets and rework directive generation — future bundle.
8. Costing and provider fallback repair — future bundle.
9. Transition/finalization orchestration — later bundle, after subservices are extracted.

The dispatcher should stay as orchestration shell while subservices are extracted one by one.
