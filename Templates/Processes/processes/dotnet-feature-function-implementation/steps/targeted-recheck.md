# Re-run focused validation after repair

Re-run the failing proof and the smallest regression checks needed to verify the repair.

This step owns the repaired feature branch decision:

- Select `feature-accepted` only when the repaired evidence satisfies the accepted behavior.
- Select `feature-repair-escalation` when proof still fails, required evidence is missing, the inherited repair target remains unresolved, or another repair would exceed this subprocess scope.
- For repair-sourced runs, re-run the same failing proof recorded by the parent repair target and include the before/after metric or assertion in the artifact.
- Return a completed process-step outcome with the selected branch outcome. Do not return `Blocked` for product proof that has been evaluated and can be escalated.
- Return `Blocked` only when an environment, permission, unavailable tool, or process-contract issue prevents recheck execution.

This is not a build/test-only step when the failed proof involved app launch, runtime rendering, browser behavior, screenshots, console output, or any visible UI acceptance criterion. In that case, this step must execute the same class of live proof after the repair:

- Launch or verify the app with a trusted runtime receipt.
- Navigate only to the confirmed URL.
- Capture browser snapshot or browser evaluation output, console messages, and screenshot.
- Exercise the repaired visible behavior enough to prove the original failure is gone and the mapped acceptance criteria still hold.
- Stop any runtime started by this step and cite the cleanup receipt.

Do not choose `feature-repair-escalation` solely because this step skipped browser or runtime proof that it had the tools and contract to run. If the proof tool, launch capability, product root, or runtime environment is unavailable, return `Blocked` with the exact missing capability or failed command. If the live proof runs and still fails, select `feature-repair-escalation` with the failing screenshot, console/runtime evidence, and the smallest next repair target.
