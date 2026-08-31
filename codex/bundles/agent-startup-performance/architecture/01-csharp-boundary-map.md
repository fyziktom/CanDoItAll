# C# Boundary Map

| Responsibility | Current/target owner | Allowed local change | Must remain outside |
|---|---|---|---|
| Filesystem facts and path validation | Infrastructure policy/writer | Operation-scoped known facts within verified interval; keep fresh mutation checks | Agent runtime, UI, global ambient cache |
| Revision query/validation | Module provider loader and existing mappers/materializer | Narrow typed projection; validation separated from effective profile copying | Persistence schema, public provider contract, credential dispatch, new service layer |
| Immediate commit and projection | Existing Persistence store/slice/chat owners | Reuse freshly validated plan under same locks; proved unaffected transformations only | Runtime progress callbacks, batching, new journals, cross-lock trust |
| Progress orchestration | Core execution service | Read-only oracle | No skipped steps, new parallelization or changed callback await |
| UI/provider/tool behavior | Existing app/runtime components | Regression proof only | UI redesign, new cancellation feature, fake tool/provider path |

Prefer method-level reuse inside the same responsibility. If a real isolated helper is needed, use a cohesive top-level internal type in its existing project and test it directly. A typed value carrying facts/validation state is acceptable; no public bypass flags, `AsyncLocal`, static mutable registry or one-trivial-implementation interface.

Contracts remain in existing Abstractions/Models; no contract relocation/new project. Composition root and direct construction remain unless a narrowly justified internal seam is necessary. No temporary bridge is planned. If extracted, delete duplicate old behavior and prove ownership; otherwise demonstrate removed redundant work with no monolith growth by unrelated responsibilities.
