# Codex QA Prompt: MAF Stabilization Verification

You are verifying an implementation of the CanDoItAll MAF stabilization bundle.

Check the repository against every requirement in `traceability/matrix.md`.

Do not trust implementation claims. Verify by reading code and running tests.

## Required verification checks

1. Search for any remaining `structuredOutput: null` continuation path that should preserve a contract.
2. Search for any workflow/process decision parsed from markdown or raw assistant text.
3. Verify disabled built-in tools are not attached.
4. Verify function invocation middleware applies policy before tool execution.
5. Verify validators exist for all machine-critical DTO families.
6. Verify invalid structured output cannot complete a machine-critical run.
7. Verify repair/retry is bounded and repaired output is revalidated.
8. Verify finalizer tools are exact-once where configured.
9. Verify session/history is not the source of truth for process status or branch selection.
10. Verify provider capability checks fail early.
11. Verify generic runtime does not include calculator-specific hints.
12. Verify logs/traces include validation, tool policy, repair, finalizer, raw output hash, and final outcome.

## Required commands

Run the relevant repository build and tests. Use the repository's actual solution/test paths.

At minimum attempt:

```bash
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
dotnet test CanDoItAll.slnx --no-build
```

If these commands fail, separate environment failures from code failures.

## Final report format

```text
Overall status: Pass / Fail / Partial
Verified requirements:
- R01: Pass/Fail + evidence
...
Build/test evidence:
- command: result
Regression risks:
- ...
Required follow-up:
- ...
```
