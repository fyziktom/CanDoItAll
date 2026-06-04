# Readiness Assessment: Should We Start Process Core Extraction?

## Verdict

Not yet. Start with an execution boundary foundation.

## Why Not Full Core Yet

The process dispatcher still directly depends on AgentFramework types and methods. A full core split now would either:

1. Pull AgentFramework abstractions into the new core, which defeats the purpose of a clean process core.
2. Force a large DTO mapping and dispatcher rewrite in one pass, which is too risky.

## What Is Safe Now

- Inventory current Process module contracts and direct dependencies.
- Add stricter architecture tests.
- Introduce a process automation execution client/facade.
- Move direct AgentFramework execution calls behind that facade.
- Introduce a minimal contracts/abstractions project only for stable identities and boundary DTOs.
- Establish next-phase cutline for real core extraction.

## What Remains Later

The next bundle after this one may start extracting pure process policies such as transition guards, run status resolution, definition linting, and artifact status projection only if this execution boundary proves stable.
