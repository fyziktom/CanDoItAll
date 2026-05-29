# Shared Implementation Prompt

Implement the selected subbundle only.

Start by reading:

1. Root `README.md`
2. `analysis/01-current-state.md`
3. `analysis/02-maf-delta-and-hardening-findings.md`
4. `requirements/01-normalized-requirements.md`
5. Current subbundle README
6. `reviews/01-execution-report.md`

Rules:

- Work outcome-first and keep the change set focused on the current subbundle.
- Preserve user-managed workflow definitions and existing template files unless the subbundle explicitly says otherwise.
- Reuse existing abstractions when they are clean; do not duplicate architecture under a new name.
- Prefer compile-time typed contracts over raw string/object runtime contracts.
- When adding C# code comments, write comments in English.
- Capture failing-first evidence when possible, then passing evidence after the fix.
- Update the execution report before stopping.
- Stop honestly at progression gates that cannot pass.
