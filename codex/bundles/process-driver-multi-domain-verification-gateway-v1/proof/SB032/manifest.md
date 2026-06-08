# SB032 Proof Manifest

## Status
- Subbundle: `SB032`
- Status: `Completed`
- Owned requirement: `REQ-013`
- Scope result: Business-analysis alpha verifier now emits typed diagnostics for missing requirements, unsupported assumptions, contradiction markers and evidence gaps over supplied text only.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverDiagnostic.cs` | `a5870d9f6cc1b8b0b9ad8e4d2d451bc3c285689424d6de6e02c393753cc3b487` |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Verification/ProcessDriverContractVersion.cs` | `d938ff8a4aa45ee220fd4537d4404b1766d98b3bd4786dab3d42c36fcc89a802` |
| `repo://src/CanDoItAll.Processes.Drivers.BusinessAnalysis/BusinessAnalysisDiagnosticRules.cs` | `947a3789d27d985b755ae8341bf30ae8acfa6371a3cf6069a30fc702bd19318f` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverBusinessAnalysisAlphaTests.cs` | `9266197db7ffb2a3b241694a80326138949803224bf5eac12a54a065beb5eb75` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` | `244f93869244dab66e07ba2e08604feff5f45a99bda29ade2ab8e16271ebee00` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/07-driver-abstraction-api-versioning-snapshot.md` | `eb21ab16f29f64d5d4e7308c566771e00dee8d1254e1b6e9b37f66d192ba3c1d` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/08-explicit-verification-gateway-design.md` | `204370fc86b1d002bbd6f303cce29c46174ad774cd0c0e76940f8bb06b5b7d01` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb032-add-diagnostics-for-missing-requirements-unsupported-assumptions-contr/README.md` | `3865c8c1a9b6d393801bd6ca647681017c1d9374ae807a2238f1bfc60f881842` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `88af3bf57b3ea696ebebf61c9be91a9d19c28f816ebcd38210d8aecb359e27af` |

## Command Transcripts
- Focused BusinessAnalysis diagnostics and contract tests: `bundle://proof/SB032/transcripts/focused-business-analysis-diagnostics-tests.txt`
- Source/no-drift/anti-stub audit: `bundle://proof/SB032/transcripts/business-analysis-diagnostics-source-scan-and-anti-stub-audit.txt`

## Source Assertions
- `ProcessDriverDiagnosticCategory` now has typed business diagnostics for missing requirements, unsupported assumptions, contradiction markers and evidence gaps.
- `BusinessAnalysisDiagnosticRules` inspects only supplied item text for explicit markers: `requirement:`, `evidence:`, `assumption:` and `contradiction:`.
- Diagnostics use bounded static messages and focused tests verify supplied secret/email/contradiction raw text does not leak into diagnostics or audit summaries.
- Driver abstraction contract version is `1.8.0`; public type count remains `34` and type-name hash remains `f92df2a77fbc8800345444c17edca2929f97328f9266dccb54d37bd4dd4781c5`.
- No runtime host, registry, selector, provider, DI, CRM, business-record mutation, HTTP, process, file, directory, DbContext, workspace, storage, UI/media, secret-like, or stub behavior was added.

## Validation Results
- Focused BusinessAnalysis diagnostics and contract tests passed: 17 passed, 0 failed, 0 skipped.
- Source/no-drift/anti-stub audit passed.
- No UI/media drift occurred.

## Closure Gate
- Entry gate: passed after SB031.
- Closure gate: passed.
- Progression decision: SB033 Gate K may proceed.
