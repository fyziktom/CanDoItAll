# Apply the diagnosis-guided repair or correct the proof plan

Inspect the diagnosed owning boundary and classify the action from evidence. For `product-repair-applied`, mutate the grounded external target before writing the final primary managed artifact. If that write is denied because no mutation receipt exists, do not treat it as a missing permission or blocker: mutate the product, read it back, run focused proof, then write final evidence. If the diagnosis identifies a product defect, implement the smallest source change, read every changed file back, run restore/build/tests plus focused proof, and select `product-repair-applied`.

If the diagnosis proves the product is already clean and the defect is only an incorrect route, selector, or proof recipe, do not manufacture a source edit. The expected control or behavior must already exist and work in the product; only the prior proof targeted it incorrectly. Run the corrected current-execution validation and runtime/browser proof, record the exact proof contract, and select `proof-only-revalidation-prepared`.

A missing expected control, missing state transition, non-working interaction, visible application error, or real browser-console exception is a product defect even when restore/build/tests are green. Repair the owning source and select `product-repair-applied`; never classify absent behavior as merely missing interaction evidence.

Do not spend this attempt on unrelated cleanup or claim final acceptance; independent QA owns that decision. A validation-only branch is not a no-op: it requires concrete current-run proof that addresses the diagnosed evidence gap. If the diagnosis remains insufficient, return a concrete blocker.

Never write `product-repair-applied` or describe changed/deleted files unless this execution has a successful product-target mutation receipt and current product readback proving those exact changes. A diagnosis artifact, intended change list, or green build/test receipt is not evidence that a repair occurred.

For a visible UI error with no matching console exception, diagnose the rendered state, style/loading path, and owning source before changing code. Preserve useful failure visibility while repairing the actual product defect; do not hide an observed failure merely to make proof look clean.
