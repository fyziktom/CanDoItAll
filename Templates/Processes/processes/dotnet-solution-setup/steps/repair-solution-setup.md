# Repair solution setup findings

Repair only the setup findings recorded by `validate-first-build`.

## Scope

- Fix scaffold, project file, package/reference, template-integrity, and test-discovery issues needed for first build proof.
- Keep changes minimal and tied to the validation failure packet.
- Do not implement feature behavior, replace starter UI beyond template repair, launch runtime, or capture browser proof.

## Output

Write the setup repair change set to `artifacts/process-runs/<current-process-run-id>/steps/repair-solution-setup.md` and include changed files, root cause, exact repair actions, commands to rerun, and remaining setup risks.
