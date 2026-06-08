# Next Roadmap Summary

## Near-term work after this bundle
1. Complete explicit gateway/adapters for artifact, Office, business-analysis, and observation aggregation lanes.
2. Burn down stale architecture fixture debt and keep full-unit proof transparent.
3. Add package-level API governance and versioning for every domain driver package.
4. Introduce read-only process evidence collection helpers that create supplied payloads but never read arbitrary files or mutate process state.
5. Prepare controlled verification workflow templates only after adapters are stable; do not wire scheduler/manager/runtime host yet.

## Medium-term work
1. Runtime host design proposal with approval gates.
2. Audit persistence design, still without implementation.
3. Read-only Office evidence review over already-produced mail/document evidence, not Graph.
4. Business-analysis gap reviewer over supplied deliverables, not CRM mutations.
5. Artifact evidence reviewer over supplied Core descriptor snapshots and artifact metadata only.

## Long-term work
1. Runtime host with explicit DI/registry only after sandbox/allowlist/audit persistence/ownership is proven.
2. Execution-capable drivers only after sandbox, command allowlist, timeout, output hash, secret masking, and side-effect lifecycle owner are approved.
