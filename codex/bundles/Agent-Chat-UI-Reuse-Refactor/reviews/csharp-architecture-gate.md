# C# architecture review gate

Record:

- snapshot ids and health;
- current and target owners;
- project references before/after;
- cycles before/after;
- large-file/type findings before/after;
- extracted contracts and implementations;
- tests that instantiate the new owner without the old runtime;
- old owner responsibility reduction;
- partial-class check;
- service-location check;
- source-switch/boolean-matrix check;
- source-neutral dependency check;
- progression decision.

The review fails when the new project is only a type bucket, the old monolith still owns the same presentation logic, or tests require the full Agent runtime.
