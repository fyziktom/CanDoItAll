# CanDoItAll plugin-wave architecture review bundle v7

- Date: 2026-04-05
- Scope: post-phase6 static architecture review of the current refactored branch
- Verdict: **NO-GO for the big connector/plugin wave**
- Runtime validation: **blocked** in this environment because `dotnet` is not installed

## What changed in v7

This bundle is stricter than the previous one because the same core blockers were still present after another refactor phase.

New forcing mechanisms in this bundle:
1. hard exit gates in `requirements/02-hard-gates.md` and `gates/02-exit-criteria.md`
2. per-item subbundles with forbidden patterns and required proof
3. a repo-level static closure script in `scripts/gate_check_phase7.py`
4. an explicit rule that ADR-only closure is not acceptable for repeated blockers
5. a senior-QA hard-gate review that treats repeated unresolved blockers as stop conditions

## Current verdict

The refactor improved some seams, especially around typed node references and CRM/HR ownership boundaries, but the branch is still not a safe base for the next large wave of plugins such as email, LinkedIn, and custom APIs.

The deepest repeated blockers are still open:
- persisted Workbench parallel truth
- overloaded universal carrier without typed facets/bindings
- fragmented node-kind semantics and node-scoped capability rules
- in-place reclassification without transition history
- hierarchy dual-write
- closed enum/switch-based connector seam
- missing hard closure mechanism

## Required use

Codex must not start the large connector/plugin wave until all hard gates pass. The closure contract is:
- implement the refactor
- add the required tests
- run the hard-gate script
- attach proof for each subbundle item
- then run a final QA pass

See:
- `analysis/04-plugin-wave-readiness.md`
- `requirements/02-hard-gates.md`
- `plan/02-closure-evidence-checklist.md`
