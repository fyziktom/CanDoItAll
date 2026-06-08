# SB011 Proof Manifest

## Status
- Subbundle: `SB011`
- Status: `Completed`
- Owned requirement: `REQ-004`
- Scope result: malicious transcript corpus covers prompt injection text, path-like text, secret-like text, oversized content, and mixed .NET/Rust markers as supplied evidence only.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/ProcessDriverContractTranscripts/malicious-prompt-path-secret-transcript.txt` | `1c6f9dea119437c0653f9b2aed7523ae639804491123c803a26707af5b8ea3a6` |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/ProcessDriverContractTranscripts/malicious-mixed-dotnet-rust-transcript.txt` | `ccd184365feda9cbe99aa5ec4cf52739d768b7c14bf04ed4a816036f069e7d73` |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/ProcessDriverContractTranscripts/malicious-oversized-transcript.txt` | `7aa744673cc4b4734f355079ee5b7900790d09cf062323323f90a35be24cc45c` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs` | `30e8efed1652b1b9eee72ee536066c0957232cea4596aa54790904b6e4abf231` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb011-add-malicious-transcript-corpus-prompt-injection-text-path-like-text-s/README.md` | `f4ac133a303dd3121d9de5a0b2de55abb0ffc407eec39cc7292355deaaf412ad` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `8db39e9fd45a8d4224c7d963246cef135325442c94fe859c611d3da3220e005c` |

## Command Transcripts
- Focused transcript verifier tests with malicious corpus: `bundle://proof/SB011/transcripts/focused-transcript-verifier-with-malicious-corpus.txt`
- Corpus/source/no-drift audit: `bundle://proof/SB011/transcripts/malicious-corpus-source-scan-and-anti-stub-audit.txt`

## Source Assertions
- The prompt/path/secret fixture includes prompt-injection text, path-like text, connection-string-like text, token-like text, password text, email text, .NET warnings, missing artifact, and runtime proof gap markers.
- The mixed fixture includes .NET and Rust markers and is validated under both transcript languages.
- The oversized fixture is longer than the default redaction limit and verifies truncation through `ProcessDriverRedactionPolicy`.
- The verifier treats all corpus inputs as supplied transcript content only; no path, file, network, workspace, or storage access is performed.

## Validation Results
- Focused transcript verifier tests passed: 9 passed, 0 failed, 0 skipped.
- Corpus/source/no-drift/anti-stub audit passed.
- No repository secret-pattern matches were introduced.
- No UI/media drift occurred.

## Closure Gate
- Entry gate: passed after SB010.
- Closure gate: passed.
- Progression decision: SB012 Gate D may proceed.
