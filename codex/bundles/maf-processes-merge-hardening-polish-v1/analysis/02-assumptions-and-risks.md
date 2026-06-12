# Assumptions And Risks

## Assumptions

- Codex will execute on branch `maf-processes-refactor`.
- The branch already has the current successful multi-team app delivery behavior.
- Old prepared/completed bundle directories are no longer desired as tracked repo content.
- `codex/skills/bundles/**` remains valid source-of-truth tooling and must not be deleted or ignored accidentally.
- The next large dispatcher-runtime isolation is explicitly deferred until after merge to `development`.

## Critical Path Risks

- If transient artifact cleanup deletes `codex/skills/bundles/**`, future bundle preparation tooling is damaged.
- If tests keep `SB###`/bundle names, temporary planning language becomes permanent test API and encourages future leaks.
- If software-delivery proof logic remains in generic dispatcher partials, the next runtime isolation will be harder and more error-prone.
- If software-delivery proof extraction is too aggressive, it can break the working Tetris/multi-team app delivery path.
- If a new domain driver uses DI, discovery, external calls, file IO, workspace writes, or process mutation, it violates the verification-only driver boundary.
- If repository scans inspect all local files instead of tracked files, ignored local Codex helper outputs can create noisy false failures.

## Validation Risks

- Full build alone will not catch naming leaks or architecture leakage.
- Unit tests alone will not prove there are no tracked transient artifacts unless they scan tracked files.
- Search-based tests can become brittle if they prohibit words like `bundle` inside the actual bundle-preparation skill. Exclude `codex/skills/bundles/**` explicitly.
- Live process validation can be expensive and environment-dependent. If it cannot be run, closure must state the exact attempted command and blocker.

## Reopen Triggers

- Any tracked path under `codex/bundles/` or `codex/bundle-exports/` remains after SB01.
- Any active test method contains `SB\d+`, `INV\d+`, `bundle`, `subbundle`, or a historical bundle slug after SB02.
- Any new MAF source/project reference points at `CanDoItAll.Modules.Processes`.
- Any Process Core project reference points to Modules, Drivers, Infrastructure, AgentFramework, EF, UI, or plugins.
- Any driver package references `CanDoItAll.Modules.Processes`, Infrastructure, EF, UI, AgentFramework, workspace/storage, or plugins.
- Any gateway gains generic lane dispatch, dynamic/object payload dispatch, service provider use, assembly scanning, or manager/scheduler/workflow hooks.
- Any software-delivery proof extraction breaks the existing process-focused integration tests or removes Tetris/multi-team delivery proof behavior.
