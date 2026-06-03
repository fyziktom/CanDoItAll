# Shared Implementation Prompt

You are implementing the `maf-processes-decoupling-bundle-v1` bundle.

Follow these rules:

1. Read `README.md`, `plan/01-phase-plan.md`, `requirements/01-normalized-requirements.md`, `inventories/01-process-tool-parity-inventory.md`, and the current subbundle README before editing.
2. Do not skip subbundle order.
3. Do not simplify tool names, access checks, or approval behavior.
4. Do not move process dispatcher logic in this bundle.
5. Do not introduce process driver packs in this bundle.
6. Preserve exact process tool names listed in the inventory.
7. Add or update tests before considering a subbundle closed.
8. For critical subbundles, create `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md`.
9. Keep command transcripts under `proof/SBxx/transcripts/`.
10. If a downstream subbundle reveals a problem in an earlier foundation, reopen the earlier subbundle.

Current target architecture:

```text
MAF -> AgentFramework.Tooling -> IAgentRuntimeToolProvider
Processes -> AgentFramework.Tooling -> ProcessAgentRuntimeToolProvider
MAF must not reference Processes.
```

Never make a passing build by removing tests or reducing assertions.
