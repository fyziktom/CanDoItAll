# Proof artifact data review

The first artifact-secret scan intentionally remains as historical evidence: artifact-secret-scan.json and transcripts/artifact-secret-scan.log reported 20 token/assignment-shaped values across stable discovery copies. They were traced to committed synthetic security test data, not runtime credentials:

- repo://tests/Unit/CanDoItAll.Tests.Unit/SecretScanningTests.cs constructs provider-shaped samples from constant prefixes and repeated A characters.
- repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs defines synthetic spoofed-envelope and non-object JSON InlineData.
- repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs defines synthetic array/string redaction InlineData.

The final stable output must finish before sanitization. sanitize-proof.py then masks only those verified fixture display values, retaining method/provider/case identity, pass/fail outcomes and counts. Long provider arguments were already shortened by VSTest; proof-redaction.json records SHA-256 for each exact removed display fragment and the original/delivered file hashes, with source references for the complete generated fixture. Ignored compressed local raw copies under .mcp-state preserve the original runs and are not delivery artifacts.

The next full scan found two occurrences of a real disposable-host token emitted by LlmChatsApiIntegrationTests while verifying query-token rejection (HTTP 401). Unlike the static fixtures, this is credential material. redact-test-token.py removed it from the delivered transcript/gzip and the compressed local backup. test-token-redaction.json records the hash chain and explicitly qualifies that backup as credential-masked. The intermediate scanner finding remains in artifact-secret-scan-post-fixture.json.

After both redaction steps, artifact-secret-scan-final.json and its transcript scan the delivered text again. The final verifier also applies all four repository provider-key shapes, including patterns beyond the general artifact scanner. This adds a delivery check beyond Repository_contains_no_realistic_provider_keys: that existing test walks repository text but explicitly excludes codex/bundles. Its exclusions and source were not changed.

The complete sanitized stable log is delivered losslessly as final-stable-results.log.gz after scanning the full 72 MB raw text with a 200 MB limit; the redundant .log remains local and ignored. Images were visually reviewed; the compressed portability scan retains the complete JSON whose raw source was included in the text scan. Browser reports retain bounded fixture identities and UI outcomes. One incidental runtime token in the Governance UI text was masked when serializing browser-final-actions.json. No credentials, connection strings, database dumps or package/runtime caches are intended deliverables.

