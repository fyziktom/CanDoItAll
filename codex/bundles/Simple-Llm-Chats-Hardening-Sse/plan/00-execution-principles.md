# Execution principles

1. Work only on the current unlocked subbundle.
2. Reinspect current source and exact tests before editing.
3. Keep implementation and proof tied to one commit.
4. Critical invariants require real PostgreSQL failure/race/crash evidence.
5. Provider calls never occur inside database transactions.
6. No automatic redispatch after possible paid dispatch without proof it did not begin.
7. Avoid partial-class growth and service-location.
8. Prefer cohesive command/query/reducer/adapter types over one universal service.
9. Run the narrowest affected tests after each coherent change.
10. Run the broad stable gate only in SB13.
11. Update status, traceability and proof immediately after each closure.
12. Reopen earlier work whenever later streaming evidence changes its semantics.
