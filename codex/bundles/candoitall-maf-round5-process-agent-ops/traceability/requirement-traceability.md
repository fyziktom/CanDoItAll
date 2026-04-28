# Requirement Traceability

| Requirement | Subbundles | Key evidence to verify |
|---|---|---|
| R01 | 00, 12 | appsettings contains no key pattern; secret tests exist; execution report matches files/tests |
| R02 | 01 | continuation paths preserve structured output; finalizer exact-once tests pass |
| R03 | 02 | process mutation tools classified; disabled tools not exposed; policy exception boundary |
| R04 | 03 | `AgentRecoveryDecision` persisted and tested |
| R05 | 04 | `AgentReworkPacket` generated from QA/proof/manual cases |
| R06 | 05 | proof fingerprints and invalidation tests |
| R07 | 06 | retry ledger/backoff/loop detection tests |
| R08 | 07 | escalation aggregate/service/UI actions |
| R09 | 08, 09 | operator controls, attempt timeline, approval/rework console tests |
| R10 | 10, 11, 12 | extracted services, focused tests, stable gates |
