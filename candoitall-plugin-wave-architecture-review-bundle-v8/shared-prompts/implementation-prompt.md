Use this bundle as a hard-gated phase8 architecture refactor.

Work in this order:
1. close P8-002 and P8-001 first,
2. then P8-003,
3. then P8-005,
4. then P8-006,
5. finally advisory cleanup like P8-004 and P8-007.

Rules:
- do not claim closure until the symbol-retirement gates are actually satisfied
- update tests as you go
- run the bundle gate script after each hard-gate stream
- record proof, not promises
- if a transition requires compatibility code, isolate it and give it an explicit retirement path
