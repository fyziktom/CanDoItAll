# Executor guide — Claude Code primary, model-neutral execution

## Preferred executor

- Claude Code
- Claude Fable 5
- Deepest/maximal reasoning mode available (`xHigh` is intent, not a literal required flag)
- Fallback: best configured high-capability Claude model; Codex may execute the same subbundles with the Codex prompt

## Mandatory startup for every subbundle

1. Read the root review verdict, findings register, execution order, relevant architecture notes, and the entire subbundle README.
2. Read `sharedinfo/required-skills.md` and invoke the installed architecture skills.
3. Confirm exact repository, branch, HEAD, working tree, and dependency unlock state.
4. Create `proof/proof-manifest.json` from the template and `proof/SESSION-HANDOFF.md` before risky edits.
5. Build/reuse the narrowest CodeAnalytics snapshot and record its ID, dashboard health, cycles, findings, and exact symbols.
6. Add characterization/failing tests before production changes.

## Working rules

- Work one bounded subbundle only.
- Inspect exact source and project files; do not rely on bundle prose as source of truth when the branch changed.
- Source-code comments and identifiers must be English.
- Prefer direct constructor injection and top-level cohesive types.
- Never create partial-class architecture or broad Helpers/Managers.
- Never widen authority, path access, approval, or process semantics to make tests pass.
- Do not commit/push/open a PR unless explicitly requested.

## Durable handoff

At every session end, update `proof/SESSION-HANDOFF.md` with current commit, changed files, completed tasks, commands/results, CodeAnalytics evidence, unresolved defects, exact next action, and decisions that a fallback model must preserve.
