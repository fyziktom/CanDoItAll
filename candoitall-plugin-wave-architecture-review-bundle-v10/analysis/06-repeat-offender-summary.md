# Repeat-offender summary

## What keeps repeating
Across v7, v8, and now the claimed v9 closure, the repeated failure mode is:

1. a real architecture problem is identified,
2. the closure package focuses on the current symbol shape,
3. Codex retires or renames the targeted symbols,
4. the behavioral invariant remains partially broken,
5. the gate still turns green because it was not checking behavior.

## Why bundle10 is intentionally different
Phase10 is built around **behavior closure**, not just symbol retirement.

That means Codex must now prove:
- the read seam is zero-write,
- cleanup moved to an explicit maintenance boundary,
- tests demonstrate zero-write behavior under stale data,
- the new gate detects transitive write helpers instead of only old method names.

## Explicit lesson from previous bundles
- **v7** introduced stricter guardrails because earlier reviews were too soft.
- **v8** still produced a false green because its checks were too narrow.
- **v9** improved repo-wide symbol retirement but still missed a behavioral violation in the active read path.

## Design principle for phase10
If Codex can pass phase10 without proving behavior, then phase10 is badly specified.  
This bundle is written so that behavior proof is mandatory and the current false-green pattern is much harder to repeat.
