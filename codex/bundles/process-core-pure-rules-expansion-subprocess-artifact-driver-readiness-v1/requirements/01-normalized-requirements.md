# Normalized Requirements

| ID | Requirement | Owning phase | Proof |
| --- | --- | --- | --- |
| REQ-001 | Preserve the existing narrow Core seed and its route rule behavior. | Phase 1 | Build, architecture tests, route parity tests |
| REQ-002 | Add only pure deterministic Core families; no broad process-core extraction. | All phases | Core forbidden dependency scan |
| REQ-003 | Move subprocess lifecycle pure status/reason facts into Core, keeping runtime orchestration module-local. | Phase 2 | Subprocess lifecycle parity tests |
| REQ-004 | Move subprocess artifact source mapping pure rules into Core, keeping projection persistence/gap journals module-local. | Phase 3 | Artifact mapping parity tests |
| REQ-005 | Introduce Core artifact expectation snapshot/read model and keep storage/projection/validation writes outside Core. | Phase 4 | Snapshot parity tests |
| REQ-006 | Move only pure artifact expectation matching/satisfaction descriptors into Core. | Phase 5 | Matching/satisfaction parity tests |
| REQ-007 | Add module-local adapters from existing process models to Core read models. | Phase 6 | Adapter boundary tests |
| REQ-008 | Keep finalizer, transition, claim, AgentFramework, workspace/storage/filesystem, EF, and process mutation out of Core. | All phases | Forbidden dependency/token scans |
| REQ-009 | Prepare future process-helper-driver contract proposal only as docs/tests, with no production API. | Phase 8 | Driver no-production scan |
| REQ-010 | Keep subbundle rows individual and proof gates meaningful. | All phases | Completed-stage validator |
| REQ-011 | Do not create small/medium/mobile/browser proof for runtime/service-only changes. | All phases | No UI/media/source diff scan |
