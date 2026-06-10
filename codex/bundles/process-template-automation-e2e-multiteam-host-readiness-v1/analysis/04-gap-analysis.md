# Gap Analysis Toward Reliable Process Execution

## Product/runtime gaps
1. Representative templates need actual automation dispatch proof, not only manual transition proof.
2. Multi-team development needs a source-backed launch scenario and explicit multi-role/multi-team governance assertions.
3. Blazor/.NET template needs a runtime automation harness that routes through outbox/dispatch/finalizer and records managed artifacts.
4. Business-analysis template needs the same automated-runtime proof without software/.NET leakage.
5. Project-structure launch/readback should be exercised with representative templates, not only generic smoke.
6. Manager/operator runtime-host readback should be tied to real process runs and step runs.
7. Scheduler/workflow read-only verification jobs need lifecycle state, provenance, audit readback, and integration tests.

## Runtime-host gaps
1. Dry-run host is still isolated from template execution scenarios.
2. Capability descriptors are static and safe, but not yet used broadly in operator/readback flows.
3. Execution-capable drivers remain correctly blocked. The next phase should produce stronger dry-run/readiness evidence, not execute effects.

## Refactor regression risks
1. Template names/keys may drift from UI/catalog expectations.
2. Process Core could accidentally receive domain/runtime concepts if more code is moved too aggressively.
3. Dispatch/finalizer can be bypassed by manual transition tests, masking runtime regressions.
4. Project-structure writeback can appear green if only service-level artifact records are checked.
