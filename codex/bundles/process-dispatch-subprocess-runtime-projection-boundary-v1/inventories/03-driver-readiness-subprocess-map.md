# Driver Readiness Subprocess Map

Documentation-only.

| Future driver concept | Existing process runtime meaning | Do now? |
| --- | --- | --- |
| `DelegatedProcessEvidence` | A child subprocess run completed and produced evidence for a parent step. | Document only |
| `SubprocessRunOutcomeEvidence` | Child run status maps to parent step status. | Document only |
| `SubprocessArtifactProjectionEvidence` | Child artifact was projected into parent run artifact ledger. | Document only |
| `CapabilityGapEvidence` | Child active steps have missing roles/capability gaps. | Document only |
| `ParentScopedArtifactProjection` | Parent-scoped durable artifact generated from child artifact. | Document only |

Do not create production driver APIs in this bundle.
