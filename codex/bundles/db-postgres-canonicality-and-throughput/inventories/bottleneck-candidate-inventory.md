# Bottleneck candidate inventory

| Candidate | Evidence | Risk | Priority |
|---|---|---|---|
| Dead switch/drain state | `DatabaseRuntimeSwitching.cs` still exposes context lease and switch session | Future accidental reintroduction of hot-switch bottleneck | High |
| Sequential claimed work | automation/process/connector outbox claim batches then loop sequentially | PostgreSQL concurrency not fully used | High |
| Process claim token not enforced at final mutation | renewal failure logs warning only | stale worker may commit | Critical |
| Candidate loading before claim | `LoadDispatchCandidateAsync` hydrates much of run before claim | high DB load and lock race window | High |
| Pending activation vs runtime display | restart-first activation creates two states | UI/API confusion | Critical |
| `EnableMaintenanceHotSwitch` option | option exists but no hot switch happens | misleading configuration | Medium |
| Long-lived `StepDispatchGuards` dictionary | process-local guard remains and may leak keys | memory growth; misleading ownership | Medium |
| Non-pooled profile-specific contexts | maintenance paths create raw options/context per profile | acceptable for maintenance, bad if used in runtime | Medium |
