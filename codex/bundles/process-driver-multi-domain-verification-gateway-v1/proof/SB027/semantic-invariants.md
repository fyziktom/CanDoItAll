# SB027 Semantic Invariants

## SB027_INV_001
- Invariant ID: `SB027_INV_001`
- Source raw note: `Prepare broader phases toward stable Core and domain drivers`.
- Expected behavior: Gate I can close only when accepted and denied transcript/runtime verification responses carry diagnostics, no-mutation status, normalized audit facts, bounded audit summaries, redaction descriptors, and non-leak assertions.
- Disallowed shallow implementation: accepted-only response proof, checking only `NoMutationPerformed`, helper-only assertions that do not exercise gateway-visible responses, missing denied response audit facts, missing redaction descriptor proof, or unbounded/unredacted diagnostic summaries.
- Failing-first test: `bundle://proof/SB027/transcripts/red-team-audit-redaction-shallow-proof-rejection.txt` rejects closure without accepted and denied transcript/runtime coverage, audit fact assertions, redaction assertions, non-leak assertions, bounded summary proof, focused pass count, and no-side-effect source scan.
- Passing test: `bundle://proof/SB027/transcripts/gate-i-proof-index.txt` verifies SB025/SB026 manifests, clean build, 40/40 focused tests, Gate I source scan, red-team rejection, and this invariant contract.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarness.cs`; `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarnessTests.cs`; `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Audit/ProcessDriverAuditFact.cs`; `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Audit/ProcessDriverRedactionPolicy.cs`.
- Production assertions: normalized audit fact and redaction contracts from SB025/SB026 are exercised through actual gateway-visible transcript/runtime responses.
- Security assertions: denied transcript response proof includes secret/email redaction and diagnostic/audit non-leak assertions; source scan proves no runtime host, registry, selector, provider, DI, process, HTTP, file, directory, DbContext, manager command, UI/media, or secret-like drift in Gate I targets.
- Adversarial negative case: a closure that checks only accepted responses or only `NoMutationPerformed` is rejected with simulated verifier exit code 1.
- Downstream dependency check: SB028 and later domain verifier phases may proceed only from response proof that covers both accepted and denied outcomes; if audit/redaction/no-mutation coverage fails, reopen SB025-SB027.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Normalized audit fact | Transcript/runtime verifiers and process preflight producers | Gateway-visible verification responses and downstream audit/redaction phases | Created for every accepted or denied requested operation with caller, lane, operation, evidence references, denial, summary, and output hash | `Process_driver_verification_gateway_SB027_INV_001` |
| Redaction descriptor | Central redaction policy and verifier response builders | `ProcessDriverVerificationResponse.Redaction` and audit facts | Produced for every response; denied transcript proof carries secret/email redaction and non-leak assertions | `Process_driver_verification_gateway_SB027_INV_001` |
| Mutation-free response envelope | Transcript/runtime verifiers and verification gateway | Future domain verifier phases | Returned for accepted and denied responses; side-effect operations are denied without mutation | `bundle://proof/SB027/transcripts/gate-i-focused-audit-redaction-no-mutation-tests.txt` |
