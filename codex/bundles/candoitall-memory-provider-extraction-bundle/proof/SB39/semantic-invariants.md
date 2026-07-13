# SB39 Semantic Invariants

## SB39-INV-01 - Authorization And Project Isolation Precede Materialization

- Expected: every memory operation except an intentionally minimal liveness seam has an authenticated caller and validated project authorization; recall applies lifecycle, access, redaction, project, source, agent, and session policy before content reaches mapping or logs.
- Shallow implementation: parse an arbitrary caller project, silently fall back to global scope, or filter forbidden records after DTO construction.
- Evidence: `bundle://proof/SB39/transcripts/anti-stub-audit.txt`, external 59/59 test aggregate, and service/domain/persistence hash anchors.
- Negative cases: anonymous/invalid credential, missing/malformed/wrong project, project swapping, restricted/redacted/unapproved records, and foreign session/source scope.
- Downstream: SB40 exercised this invariant through the actual main remote driver process seam.

## SB39-INV-02 - The External Repository Owns A Portable Wire Boundary

- Expected: the external solution builds and tests with no main checkout present; Contracts owns provider-neutral Protocol v1 DTOs without project or package dependencies.
- Shallow implementation: retain a sibling `ProjectReference`, copy main implementation namespaces into the service, or verify compatibility only by compiling both repositories together.
- Evidence: `bundle://proof/SB39/transcripts/external-local-and-isolated-validation.txt`, `bundle://proof/SB39/transcripts/codeanalytics-and-boundary-audit.txt`, and checked-in fixture hashes.
- Negative cases: absent sibling repository, project-graph audit, forbidden root/namespace scan, and schema-fixture conformance.

## SB39-INV-03 - Advertised Capability Must Have Hosted Behavior

- Expected: the service advertises only implemented operations and returns typed, authenticated protocol outcomes over HTTP.
- Shallow implementation: advertise operation status, feedback, source request, event, RCL, or advanced semantics backed only by placeholder, always-failed, or `501` routes.
- Evidence: hosted security/protocol tests within the 59/59 aggregate and `bundle://proof/SB39/transcripts/anti-stub-audit.txt`.
- Negative cases: unsupported operation/capability and malformed request behavior.
- Proof boundary: raw hosted HTTP and shared JSON fixtures are proven in SB39; actual main `NativeRemoteMemoryProviderDriver` interoperability passed in SB40.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Authenticated request context | Service security boundary | Application/domain/store | hosted request to scoped recall | invalid caller/project denied before engine access |
| Protocol v1 JSON | external Contracts/fixtures | hosted API and main HTTP driver | serialize/request/deserialize | malformed/unsupported request |
| Recall candidate | scoped persistence/access policy | response mapper | query/filter/materialize | forbidden content never mapped or logged |

## Validator Invariant Contract

- Invariant ID: SB39-EXTERNAL-ISOLATION
- Source raw note: Cognitive Memory is an independent optional repository and any main agent must access it through a secure provider protocol.
- Expected behavior: external auth/project/access policy fails closed, Protocol v1 is externally owned, and the external solution builds/tests without the main checkout.
- Disallowed shallow implementation: anonymous/global fallback, post-materialization filtering, sibling project references, or a manifest backed by placeholder routes.
- Failing-first test: failing-first N/A for this process reconstruction because a complete pre-repair hosted transcript was not retained; no production failure is fabricated.
- Passing test: bundle://proof/SB39/transcripts/external-local-and-isolated-validation.txt and bundle://proof/SB40/transcripts/terminal-validation.txt.
- Changed source files: external Protocol v1 contracts, service security/authorization, access policy, persistence projections, and hosted tests recorded in bundle://proof/SB39/transcripts/file-hashes.txt.
- Production assertions: authenticated claims bind project scope before the access filter and only implemented capabilities are advertised.
- Red-team negative case: anonymous, invalid credential, wrong/malformed project, project swap, restricted/redacted data, malformed wire, and missing sibling checkout all fail.
- Downstream dependency check: the real main NativeRemote driver completed a query/ledger lifecycle against the launched external service.
