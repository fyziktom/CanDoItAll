# Run focused validation

Run the agreed focused build and test proof and record commands, exit codes, and relevant output. For .NET product changes, run the grounded solution or project `dotnet build` and focused `dotnet test` commands from the validation contract unless the contract names a narrower equivalent command. Do not accept from upstream prose alone.

When the validation contract uses `--no-restore` build/test checks, make sure this validation execution has a fresh successful restore receipt first. If a build fails in `ResolvePackageAssets` or another stale asset-file path before compiling product source, rerun restore and then rerun the build before choosing the repair branch.

Required validation receipts must be emitted by this validation step execution. Upstream implementation receipts may explain what happened, but they do not satisfy this step's current-execution proof contract by themselves.

Read the `code-change` artifact before running or judging proof. If that artifact is `Status: Completed` but records failed build/test proof, compile errors, test failures, missing imports, or incomplete implementation, use those current-run receipts as validation evidence and choose `feature-repair-required` unless your own rerun proves the failure is already repaired.

Write or update `artifacts/process-runs/<current-process-run-id>/steps/targeted-validation.md` with the final evidence before returning. Do not leave this artifact as `in progress`. If any proof command ran, cite the targeted-validation artifact and the generated command receipt/output refs in `evidenceRefs`; do not return `Blocked` with empty evidence refs after proof artifacts exist.

When selecting a validation branch, put the exact selected key on one line near the top of the artifact as `Branch outcome key: feature-accepted` or `Branch outcome key: feature-repair-required`. Do not write only a heading such as `## Branch outcome key` with the key on the next line.

When visual target ImageAsset ids or media paths are part of the feature boundary, verify that the change set and tests preserve those acceptance inputs. Live screenshot comparison belongs to the parent runtime-command and screenshot writeback steps that own launch, browser, screenshot, and image-analysis tools.

This step owns the feature validation branch decision:

- Select `feature-accepted` only when current-execution build/test command receipts emitted by this validation step and the targeted-validation artifact prove the focused behavior satisfies the accepted behavior.
- Select `feature-repair-required` when build/test receipts are missing, proof fails, the completed code-change artifact records failed proof, artifacts are missing, validation commands fail, implementation is incomplete, or evidence does not map to the accepted behavior.
- For repair-sourced runs, the focused proof must include the inherited repair target. Do not accept a different behavior while the triggering defect remains untested or failing.
- Return `Status: Completed` in the targeted-validation artifact and final process-step outcome with the selected `BranchOutcomeKey`. Do not write `Status: Blocked` when selecting `feature-repair-required`; product proof failure is a completed validation decision that routes repair.
- Return `Blocked` only when an environment, permission, unavailable validation tool, or process-contract issue prevents validation or prevents repair from being requested inside this subprocess. If build/test receipts or source-read receipts already exist, use them as evidence and choose `feature-repair-required` for incomplete proof instead of blocking.

Keep focused validation bounded. Use a `workspace_dotnet_test` timeout of 300 seconds or less for generated or focused tests unless a current diagnostic proves the test suite needs more time. If validation hangs or times out, record the command and timeout as failing proof and request targeted rework instead of waiting on a broad unbounded run.

Do not launch the app or open a browser in this atomic validation step. Parent runtime-command and screenshot writeback subprocesses own runtime launch, browser interaction, screenshot capture, and visual comparison.
