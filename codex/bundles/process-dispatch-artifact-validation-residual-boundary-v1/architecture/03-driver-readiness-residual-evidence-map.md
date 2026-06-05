# Documentation-Only Driver Readiness Map

This bundle must not create production driver APIs. It may only document future driver vocabulary.

| Future evidence/driver concept | Current runtime surface | Production API now? |
| --- | --- | --- |
| `ProviderNativeBrowserEvidence` | Browser MCP output files and browser tool receipts | No |
| `CriticalToolFailureSuppressionEvidence` | Failed/denied/timed-out tool receipts and superseding success receipts | No |
| `ArtifactKindClassificationEvidence` | Artifact path/content type/name hints | No |
| `StorageContentKindEvidence` | Content type + extension inference | No |
| `RunnableHostEvidence` | Existing .NET host evidence helper from previous bundle | No |
| `BusinessDeliverableEvidence` | Concrete deliverable/source path rules | No |
| `ManagerVerificationEvidence` | Future manager read-only verification mode | No |

This documentation is only to prepare future driver packs after module-local boundaries stabilize. Do not add `IProcessDriverPack`, driver registries, driver packages, or public process-driver contracts in this bundle.
