# Architecture protocol

Before changing project references or a large component cluster:

1. build a scoped CodeAnalytics snapshot;
2. verify dashboard health;
3. inspect solution and project inventory;
4. inspect dependencies and cycles;
5. inspect findings/hotspots;
6. read exact target source, project files, CSS, and representative consumers;
7. record current and target responsibility ownership;
8. select the smallest justified pattern;
9. define isolated tests;
10. define what proves the old owner actually lost responsibility.

After implementation:

1. rebuild/refresh the scoped snapshot;
2. rerun dependency/cycle analysis;
3. inspect exact new contracts and consumers;
4. prove the neutral owner without full Agent runtime;
5. prove the Agent adapter retains current behavior;
6. verify no partial-class growth or service location;
7. run `csharp-architecture-review-gate`;
8. record pass/reopen/repair/block.

A successful build alone is not architecture proof.
