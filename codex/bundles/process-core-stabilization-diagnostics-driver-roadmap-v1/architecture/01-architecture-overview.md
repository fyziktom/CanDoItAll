# Architecture Direction

## Current architecture state
The process system now has a narrow Core project that owns deterministic rules/read models only. The process module continues to own application orchestration and side effects.

## Next architecture target
Move from “Core exists and compiles” to “Core is stable, explainable, and ready for a future driver contract discussion.”

## Next safe Core candidates
1. Diagnostic result models for existing Core decisions.
2. Transition intent facts that do not execute transitions.
3. Additional artifact matching/satisfaction diagnostics.
4. Public API snapshot/compatibility tests.
5. Test-only driver contract proposal artifacts.

## Explicitly forbidden in this bundle
- Broad process runtime extraction.
- EF/database movement into Core.
- Workspace/storage/filesystem movement into Core.
- AgentFramework execution movement into Core.
- Finalizer application movement into Core.
- Claim lifecycle movement into Core.
- Production driver interfaces, registries, DI, manager tools, runtime selectors, or driver execution.
