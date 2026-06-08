# Evidence Payload And Hash Policy

## Policy
- The verifier must not open arbitrary files.
- A process-module boundary may resolve evidence content only through explicitly approved evidence sources.
- The first implementation should prefer supplied transcript content in tests and controlled service calls.
- Every payload must carry a SHA-256 hash and the verifier must compare the supplied content to the hash.
- Hash mismatches return denial/diagnostic output, not exceptions that bypass audit output.
- Sensitive transcript content must not leak into audit summary or diagnostics.

## Approved Evidence Source Kinds
- Existing process proof transcript already supplied as text.
- Existing artifact/proof payload already resolved by module-owned evidence service.
- Test fixture payloads under unit/integration tests.

## Denied Evidence Source Kinds
- Arbitrary local file paths.
- Workspace globbing.
- External URLs.
- Office/Graph/mail/task/document connectors.
- Package restore output produced by running commands in this bundle.
