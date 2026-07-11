# Apply the diagnosis-guided repair or correct the proof plan

Inspect the diagnosed owning boundary and classify the action from evidence. If the diagnosis identifies a product defect, implement the smallest source change, read every changed file back, run restore/build/tests plus focused proof, and select `product-repair-applied`.

When current diagnosis and product readback prove the stock Blazor Counter/Weather navigation, pages, ASP.NET Core link, or missing hidden `#blazor-error-ui` rule remain, execute `DotNetScaffoldRepairExecutionPlan` before writing this step artifact. Write `DotNetScaffoldRepairScript` verbatim to `DotNetScaffoldRepairScriptRef`, verify the script artifact, run it with `workspace_pwsh_run_script`, `ProductRootAlias` as `workingDirectory`, and `DotNetScaffoldRepairSideEffectManifest`, then read/stat the affected product files. Do not merely copy the plan into the artifact. The supplied helper is fingerprint-guarded and must not be used to delete a product-specific Counter or Weather feature that does not match the stock template.

If the diagnosis proves the product is already clean and the defect is only an incorrect route, selector, or proof recipe, do not manufacture a source edit. The expected control or behavior must already exist and work in the product; only the prior proof targeted it incorrectly. Run the corrected current-execution validation and runtime/browser proof, record the exact proof contract, and select `proof-only-revalidation-prepared`.

A missing expected control, missing state transition, non-working interaction, visible application error, or real browser-console exception is a product defect even when restore/build/tests are green. Repair the owning source and select `product-repair-applied`; never classify absent behavior as merely missing interaction evidence.

Do not spend this attempt on unrelated cleanup or claim final acceptance; independent QA owns that decision. A validation-only branch is not a no-op: it requires concrete current-run proof that addresses the diagnosed evidence gap. If the diagnosis remains insufficient, return a concrete blocker.

Never write `product-repair-applied` or describe changed/deleted files unless this execution has a successful product-target mutation receipt and current product readback proving those exact changes. A diagnosis artifact, intended change list, or green build/test receipt is not evidence that a repair occurred.

For a visible Blazor unhandled-error banner with no matching console exception, inspect the loaded stylesheet and the `#blazor-error-ui` visibility rule before assuming an application exception. Preserve or restore the framework error UI's hidden default state, while still treating a real exception as a product defect.
