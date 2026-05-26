# SB11 Semantic Invariants

- Invariant ID: SB11-INV-001
- Expected behavior: runtime validation, lineage, mapping, block-state classification, and recovery health responsibilities are exposed through focused Processes runtime/dispatch services while manual/API transition validation keeps using the shared finalizer-grade artifact validator.
- Disallowed shallow implementation: moving behavior into UI/template-only code, adding source-only wrappers that are not called by production transition/dispatch paths, or proving only class existence without a dependent runtime regression.
- Required proof: adversarial service-boundary proof, passing production-path regression tests, source assertions for service classes and transition call sites, anti-stub audit, and changed-file hashes.
- Positive proof: `bundle://proof/SB11/transcripts/passing.txt` covers `ProcessBlockStateClassifier_SB11_INV_001`, `ProcessHealthInvariantAuditor_SB11_INV_001`, `WorkflowSubprocessArtifactMapper_SB11_INV_001`, the SB07 shared validator regression, and the SB10 stale-lineage manual transition regression.
- Negative/adversarial proof: `bundle://proof/SB11/transcripts/failing-first.txt` proves the service boundaries operate independently from dispatch partials and are not satisfied by a source-text-only split.
