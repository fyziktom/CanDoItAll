# Semantic Invariants

- A step is not ready just because an artifact slot id exists; connected required input artifacts must have available receipts.
- A successful strategy result is not accepted when required inputs are unavailable or expected produced artifacts are missing.
- Missing required inputs do not trigger automatic same-step retry.
- Missing produced artifacts route recovery to the responsible upstream producer when one can be identified; otherwise manager action is required.
- Step execution contracts are rebuilt from runtime state at dispatch time and passed through driver abstractions.
- Runtime and dispatcher contracts remain generic and do not assume .NET/software-development-specific process semantics.
- Downstream context is bounded to contract summaries, artifact ids, content hashes, and required runtime tool names rather than raw file dumps.
- Persistence round-trips connected input receipts, produced slots, required runtime tools, and recovery route metadata.
- PostgreSQL bootstrap adoption does not mark the current process migration chain as applied unless required process runtime schema elements exist.
- The implementation does not add a new final adapter partial; new adapter prompt behavior lives in a named helper.
