# Re-run focused validation after repair

Re-run the failing build/test proof and the smallest regression checks needed to verify the repair. This step has `RunValidation`; missing build/test receipts are not a reason to escalate until you have attempted the commands yourself.

When `DotNetSolutionFileAlias` or `DotNetSolutionFile` is present, use it for restore and build proof. When `DotNetTestProjectFileAlias` or `DotNetTestProjectFile` is present, use that test project target for `workspace_dotnet_test`, preferably with `noBuild=true` after a successful solution build; do not run solution-level tests for Blazor WebAssembly targets unless no test project target exists.

This step must emit its own `workspace_dotnet_restore`, `workspace_dotnet_build`, and `workspace_dotnet_test` receipts when those commands are required by the validation contract. Upstream repair receipts are useful inputs, but they do not satisfy this step's current-execution proof contract by themselves. Do not select `feature-accepted` from upstream receipt text alone.

Before deciding the branch, call `workspace_read_file` in this recheck execution on representative owning production source and the mapped test under the grounded product alias. Managed repair/validation artifacts explain the target but do not prove the current product state. If the repair attempt produced no product mutation, use the source readback plus current build/test receipts to select `feature-repair-escalation`; do not accept merely because the unchanged scaffold still builds.

When selecting a recheck branch, put the exact selected key on one line near the top of the artifact as `Branch outcome key: feature-accepted` or `Branch outcome key: feature-repair-escalation`. Do not write only a heading such as `## Branch outcome key` with the key on the next line.

This step owns the repaired feature branch decision:

- Select `feature-accepted` only when current-execution build/test command receipts emitted by this recheck step prove the repaired evidence satisfies the accepted behavior.
- Select `feature-repair-escalation` only after you attempted the required current-execution focused build/test proof and that proof still fails, remains unmapped, the inherited repair target remains unresolved, or another repair would exceed this subprocess scope.
- If the repair artifact says validation receipts are missing, run the validation commands before deciding. Do not choose `feature-repair-escalation` merely because an upstream repair step did not run validation.
- If validation tools, the product root, or the validation contract are unavailable, return `Blocked` with the missing capability/root/contract details instead of producing a no-go packet.
- For repair-sourced runs, re-run the same failing proof recorded by the parent repair target and include the before/after metric or assertion in the artifact.
- Return a completed process-step outcome with the selected branch outcome. Do not return `Blocked` for product proof that has been evaluated and can be escalated.
- Return `Blocked` only when an environment, permission, unavailable tool, or process-contract issue prevents recheck execution.

Do not launch the app or open a browser in this atomic recheck step. Parent runtime-command and screenshot writeback subprocesses own runtime launch, browser interaction, screenshot capture, and visual comparison. Do not choose `feature-repair-escalation` only because runtime/browser proof or visual comparison is absent; if focused build/test proof passes, record live proof as a parent follow-up requirement and select `feature-accepted`.

This step is reachable only from `feature-repair-applied`. A `repair-attempt-incomplete` outcome bypasses recheck and routes directly to the parent no-go packet, so green validation of unchanged product state cannot be converted into acceptance here.
