# Structured input

## Raw request

- Review the previous Workflow MAF hardening implementation and prepare follow-up polishing work focused on MAF workflows, newer MAF alignment, plugin workflow executors, and general hardening before more features build on this foundation.

## Normalized concerns

- R1: Establish an intentional current MAF package/API baseline.
- R2: Make HITL execution position aware.
- R3: Add product approval-gate runtime behavior.
- R4: Preserve useful streaming event identity and metadata.
- R5: Add checkpoint and resume foundation with a trust boundary.
- R6: Apply artifact and payload policy consistently.
- R7: Make plugin executor observer registration deterministic.
- R8: Validate plugin permission policy against manifest capabilities.
- R9: Make runtime backend catalog and UI honest about runnable backends.
- R10: Decide the `BindAsExecutor` versus source-generated executor strategy.
- R11: Keep live external effects disabled by default in proof.
- R12: Keep final evidence concise and reproducible.

