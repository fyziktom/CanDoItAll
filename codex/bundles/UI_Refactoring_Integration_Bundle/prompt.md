# Primary Codex Execution Prompt

You are a senior .NET 10 / Blazor architect executing a governed, cross-repository
integration bundle.

## Repositories

Work in the existing sibling repositories:

```text
<workspace>/CanDoItAll
<workspace>/CanDoItAll.Components
<workspace>/CanDoItAll.FileTools
```

Read each repository's contributor/agent instructions before editing. Treat current
project files, source, tests, CI workflows, and runtime composition as authoritative.

## Goal

Integrate only the original `CanDoItAll/ui-refactoring` branch into the current
development line. Stabilize the merged Components upstream, validate FileTools, merge
current development into the original UI branch, make the smallest compatibility fixes,
and produce merge-ready proof.

## Absolute prohibition

`CanDoItAll/ui-refactoring-v2` is out of scope.

- Do not check it out for implementation.
- Do not merge or cherry-pick it.
- Do not copy files, snippets, CSS, docs, or design decisions from it.
- Do not use it as a conflict-resolution source.
- Do not recreate its toolbar, themes, navigation, canonical URLs, page redesigns, or
  design-document payload.
- You may inspect only commit identities through the supplied guard scripts to prove
  non-contamination.

If a requested behavior exists only in v2, record it as deferred and leave it untouched.

## Execution contract

1. Execute subbundles in numeric order.
2. At the start of each subbundle, refresh repository state and compare it to the
   recorded baselines. Record any movement.
3. Never edit a dirty worktree until pre-existing changes are inventoried and protected.
4. Use merge commits for the requested `development -> ui-refactoring` integration.
   Do not rewrite the colleague's branch history.
5. Keep current development semantics when resolving conflicts. Reapply only the five
   intentional original-branch deltas and the compatibility work required by current
   Components.
6. Fix upstream defects in the repository that owns them.
7. Keep FileTools independent from Components.
8. Use a single coordinated package version selected according to the bundle policy.
9. Do not accept approval/snapshot changes without a human-readable semantic review in
   the execution report.
10. Use targeted tests after each coherent change; run each broad gate only at the
    prescribed phase.
11. Browser proof must use the existing supported large-desktop profile first.
12. Record exact commands and outcomes. Never report a skipped test as passed.
13. Do not publish packages or merge/push protected branches without explicit owner
    authorization.
14. All new source-code and script comments must be in English.

## Important prescribed decisions

- Keep the current CanDoItAll SDK pin from `development`; reject the old UI-branch
  downgrade.
- Keep the UI branch's root `npm run watch` convenience command.
- Keep `.idea/` ignored.
- Load `material-symbols.css`, not the removed `material-icons.css`.
- Prefer `<Icon>` in Razor; otherwise use `.cda-material-icon` as the stable DOM/CSS/test
  hook.
- Do not mass-rename icon tokens unless a rendered glyph is proven wrong.
- Preserve source-reference development.
- Commit the generated BaseLib
  `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css` and add deterministic CI
  verification so clean source consumers, CI, and Docker do not require an implicit
  prior npm build. Keep sandbox CSS generated and ignored.
- Modernize the Podman/macOS instructions under `docs/operations/`; do not preserve the
  stale claim that sibling repositories are unnecessary.
- Update CanDoItAll CI source pins to exact final sibling commits.
- Validate package fallback with a temporary local feed and
  `UseLocalCanDoItAllLibraries=false` consistently.

## Start

Open `README.md`, then execute
`subbundles/01-freeze-scope-and-guard-v2/README.md`. Continue only when each progression
gate is satisfied. Maintain `reviews/01-execution-report.md` throughout execution.
