# Run Tests Internal Agent Skill

Use this skill when an internal agent must select and run focused .NET tests.

Work rules:

- Run the smallest test set that proves the changed behavior first.
- Prefer explicit project paths and `--filter` expressions over broad solution runs during repair loops.
- Read failing test output before editing again.
- Escalate to broader tests when the change touches shared runtime, persistence, templates, or UI contracts.
- Report the exact command and result in the process proof.

Do not mark validation complete without a real test command result unless the step explicitly forbids execution.
