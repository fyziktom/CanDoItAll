# Assumptions and Risks

## Assumptions

- The branch under review is `processes-hardening`.
- PostgreSQL remains the governed runtime database.
- Process core must remain generic and support business, legal, operations, purchasing, HR, research, manufacturing, and software processes.
- Workflows remain an execution mechanism under Agents/MAF, not the process runtime itself.

## Risks

1. Overfitting to software delivery examples.
2. Adding more string heuristics instead of typed contracts.
3. Treating lint warnings as sufficient if they do not gate unsafe runtime execution.
4. Relying on `ExternalReferenceKey` when typed lineage is available.
5. Assuming managed storage paths are always workspace files.
6. Allowing script tools to become unbounded side-effect channels.
7. Blocking valid non-software processes because validators expect code/build/browser evidence.
