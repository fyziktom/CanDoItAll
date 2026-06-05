# Requirement Traceability

| Requirement | Subbundles | Validation |
| --- | --- | --- |
| RQ-001 Preserve previous behavior | SB01, SB08, SB12, SB16, SB18 | Previous boundary smoke, focused tests, full build. |
| RQ-002 Module-local only | All | Architecture source scans. |
| RQ-003 No Process Core / no driver API | All gates | No-core/no-driver scans. |
| RQ-004 Live inventory first | SB02 | Inventory source proof. |
| RQ-005 Guardrails before movement | SB04 | Architecture tests. |
| RQ-006 Header selector | SB05-SB08 | Header parity tests. |
| RQ-007 Hydration snapshot | SB07-SB08 | Snapshot tests and source scans. |
| RQ-008 Artifact input assembler | SB09, SB12 | Artifact input parity. |
| RQ-009 Branch/dependency context | SB10, SB12 | Branch outcome parity. |
| RQ-010 Assignment/workflow route | SB11, SB12 | Workflow route parity. |
| RQ-011 Technical-agent binding | SB13-SB16 | Binding/access mutation tests. |
| RQ-012 Recovery query | SB15-SB16 | Recovery directive/execution reuse tests. |
| RQ-013 Driver readiness doc only | SB17-SB18 | Documentation and no-driver scan. |
| RQ-014 No mobile/small/medium proof | All | Proof-path scan. |
| RQ-015 Refactor gates | SB04, SB08, SB12, SB16, SB18 | Gate transcripts. |
