# SB02 Semantic Invariants

- Active source no longer contains the legacy Process dispatcher/runtime implementation.
- `CanDoItAll.Modules.Processes` is present only as a disabled module shell until rebuilt.
- Direct consumers cannot start Process runs through the old `ProcessesService` path.
- Scheduler and project-structure process launch paths fail explicitly while the Process module is rebuilt.
- Process skeleton project references follow the v3 boundary order.
- Concrete Process driver projects are absent from the active tree.
- `Templates/Processes` remains as migration input and is not blindly deleted.
- Historical Process migration files remain allowed historical evidence; they are not active runtime code.
