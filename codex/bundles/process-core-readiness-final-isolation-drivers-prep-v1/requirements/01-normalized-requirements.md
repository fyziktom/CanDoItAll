# Normalized Requirements

The source requirement list is preserved in `bundle://requirements/01-requirements.md`.

| Requirement | Normalized statement |
| --- | --- |
| REQ-001 | Do not create `CanDoItAll.Processes.Core`; the default decision is no Core split. |
| REQ-002 | Preserve all current process dispatch behavior. |
| REQ-003 | Keep fewer, broader subbundles that each own coherent multi-file isolation work. |
| REQ-004 | Advance route services, route models, hydration, subprocess runtime/projection, finalizer/failure closure, contracts, and driver readiness. |
| REQ-005 | Keep production process-driver APIs out of scope. |
| REQ-006 | Keep UI/browser proof N/A unless UI source changes unexpectedly. |
| REQ-007 | Preserve the documented route stage order exactly. |
| REQ-008 | Reduce adapter-heavy boundaries through module-local services and smaller bridge APIs. |
| REQ-009 | Prove parity with focused unit tests, focused integration tests, build, scans, anti-stub checks, and route-order scans. |
| REQ-010 | Produce a final Core/driver readiness matrix with a concrete next-bundle recommendation. |
