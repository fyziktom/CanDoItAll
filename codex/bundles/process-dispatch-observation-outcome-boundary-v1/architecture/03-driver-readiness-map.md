# Driver Readiness Map

Documentation-only. Do not implement production driver APIs.

| Future concept | Current runtime meaning | Readiness action now |
| --- | --- | --- |
| `ToolObservationEvidence` | Successful tool call/result/file observation from session JSON or execution log. | Normalize observation vocabulary only. |
| `BrowserOutputEvidence` | Browser MCP output files from session/log/result summary. | Normalize browser output facts only. |
| `DeclaredOutcomeEvidence` | Structured governed process step outcome in response text. | Normalize parser/branch facts only. |
| `CompletionDecisionEvidence` | Internal rule result explaining status/reason/blockers. | Keep internal snapshot/rule model only. |
| `DriverProducedArtifactEvidence` | Future driver output satisfying artifact expectation. | Document only; do not add API. |
| `ManagerVerificationObservation` | Read-only helper observations for process manager verification. | Document only; do not expose production driver surface. |
