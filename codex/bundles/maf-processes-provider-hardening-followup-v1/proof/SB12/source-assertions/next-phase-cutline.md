# Next-Phase Cutline

The next bundle may prepare process contracts/core foundation only.

Allowed next-phase work:

- Inventory current `CanDoItAll.Modules.Processes` domain contracts, DTOs, persistence boundaries, and runtime-service dependencies.
- Propose a minimal contracts/core extraction plan with typed interfaces, source ownership, migration order, and compatibility tests.
- Add architecture guards that prevent new direct MAF product-tool dependencies.
- Add parity tests for any contract movement before moving production behavior.

Disallowed next-phase work:

- No process driver packs or external process runtime drivers.
- No new hidden MAF reference to `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.Projects`, or `CanDoItAll.Modules.Workbench`.
- No public process tool rename, removal, or access-policy weakening.
- No UI rewrites, unrelated module cleanup, or generic refactor sweeps.
- No silent fallback behavior to mask missing provider registrations, missing receipts, or missing artifacts.

Exit criteria for the next bundle:

- A prepared bundle with requirement traceability, phase gates, source inventory, and proof plan exists before production movement starts.
- Any extraction step proves compile, provider composition, process runtime smoke, artifact lineage, and access-policy parity.
- Driver-pack work remains a later bundle after contracts/core foundation is proven.
