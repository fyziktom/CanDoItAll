# Repair validation findings

Repair concrete defects, missing workflows, failed validation, or proof gaps identified by QA without expanding beyond the approved delivery scope.

## Contract
- Inputs: QA repair-required disposition, reviewed implementation package, and failing proof details.
- Outputs: Repaired change set and validation notes ready for QA recheck.
- Evidence: Changed files or deliverables, repair rationale, rerun validation, and remaining risks.
- Operation target scope: `ExternalProductTargetMutable`

When the QA finding is about runtime behavior, screenshots, browser state, console output, or launch/cleanup evidence, repair the concrete defect and rerun the smallest runtime or browser proof that demonstrates the same failing behavior is fixed. Capture current-run managed artifacts for that proof, including screenshot or browser state evidence and console output when a visible browser workflow is involved. Stop any runtime started only for this repair step before finalizing the artifact.

When the QA finding or runtime completion gate identifies stock .NET or Blazor starter scaffold in a visible UI product, repair the product files before returning. Runtime gate findings such as `process.adapter.product_required_file_content_missing` are authoritative repair targets, not documentation-only findings. Remove or replace the concrete scaffold from the shipped entrypoint and referenced pages: default counter/weather routes, default navigation links, sample weather data references, hello-world copy, and framework documentation links are defects unless the user explicitly requested them. For Blazor starter pages, deleting Counter.razor or Weather.razor is acceptable when they are default scaffold pages; placeholder pages that keep `@page "/counter"` or `@page "/weather"` still publish forbidden product routes and do not satisfy the repair. For each configured content check, inspect the current file, mutate the product file, read it back in the current run, and verify the forbidden text is absent or required text is present before writing the final repair artifact. Do not complete by only describing the issue, rewriting managed artifacts, or repeating prior validation summaries.

Validate the repaired app from the product root with restore, build, tests when present, and the smallest runtime/browser proof needed for the affected workflow. Use grounded external-target aliases, managed process refs, project-structure node ids, and current-run tool receipt refs in the repair artifact; do not copy native absolute product paths from diagnostics or launch variables into final evidence.
