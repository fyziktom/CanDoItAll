# Normalized requirements

| ID | Requirement | Owning phase |
| --- | --- | --- |
| RQ-001 | Verify and preserve the last projection coordinator split behavior. | SB01-SB04 |
| RQ-002 | Continue module-local dispatcher isolation; do not introduce Process Core. | All |
| RQ-003 | Do not introduce production process-driver APIs; keep driver readiness documentation-only. | All, SB57-SB60 |
| RQ-004 | Split `IProcessArtifactProjectionHost` into narrow, source-family-friendly facets. | SB05-SB48 |
| RQ-005 | Prevent coordinators from depending on `ProcessRunAutomationDispatchService` or a broad host surface. | SB33-SB48 |
| RQ-006 | Preserve projection source-family order exactly. | SB12, SB44, SB56 |
| RQ-007 | Preserve artifact identity, external reference keys, lineage, storage placement, trust/sensitivity and candidate mutation. | SB16-SB56 |
| RQ-008 | Preserve all existing focused unit/integration projection tests and add/keep negative guards for shallow simplification. | SB53-SB56, SB61-SB64 |
| RQ-009 | Keep browser validation N/A and avoid small/medium/mobile proof artifacts unless UI drift appears, which is prohibited. | All |
| RQ-010 | Produce portable proof artifacts and completed execution report. | SB61-SB72 |
