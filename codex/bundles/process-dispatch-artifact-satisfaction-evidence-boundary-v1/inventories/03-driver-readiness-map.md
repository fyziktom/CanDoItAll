# Driver Readiness Map

Documentation-only. Do not create production driver APIs.

| Future driver concept | Existing runtime meaning | Current helper family | Do now? |
| --- | --- | --- | --- |
| `RequiredArtifactSatisfactionEvidence` | Required artifact is satisfied by current-run artifact/evidence | artifact satisfaction helpers | Document only |
| `ProviderNativeBrowserEvidence` | Browser MCP output exists and maps to expected evidence | provider-native evidence facts | Document only |
| `ResponseTextDeliverableEvidence` | Assistant response can be projected into a declared/fallback artifact | response text satisfaction helper | Document only |
| `QualityValidationEvidence` | Build/test/validation evidence is warning-free and non-zero-test | quality validation aggregator | Document only |
| `ExternalTargetGroundingEvidence` | Response/files reference only allowed external target aliases | external target guard | Document only |
| `ManagedArtifactPathEvidence` | Paths are run-specific and not shallow shared managed roots | shallow managed path guard | Document only |
| `DocumentDeliverableEvidence` | Document deliverable exists and matches expectation | not production yet | Document only |
| `SpreadsheetDeliverableEvidence` | Spreadsheet deliverable exists and matches expectation | not production yet | Document only |
| `BusinessAnalysisDeliverableEvidence` | Analysis output satisfies non-SW process expectation | not production yet | Document only |
