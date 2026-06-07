# Requirement Traceability

| Requirement | Raw input | Owning subbundles | Planned proof |
| --- | --- | --- | --- |
| REQ-001 | Completed narrow Core seed on `maf-processes-refactor` must be preserved. | SB001-SB003 | Branch audit, build, route/core boundary scans |
| REQ-002 | Add only pure deterministic Core families; no broad extraction. | All phases | Core forbidden dependency scan |
| REQ-003 | Move subprocess lifecycle pure status/reason facts into Core. | SB004-SB006 | Subprocess lifecycle parity proof |
| REQ-004 | Move subprocess artifact source mapping pure rules into Core. | SB007-SB009 | Artifact source mapping parity proof |
| REQ-005 | Introduce Core artifact expectation snapshot/read model. | SB010-SB012 | Snapshot parity proof |
| REQ-006 | Move only pure artifact expectation matching/satisfaction descriptors into Core. | SB013-SB015 | Matcher/satisfaction parity proof |
| REQ-007 | Keep adapters module-local. | SB016-SB018 | Adapter boundary proof |
| REQ-008 | Keep side-effectful process behavior outside Core. | All phases | Forbidden dependency and token scans |
| REQ-009 | Prepare driver readiness safely as docs/tests-only. | SB028-SB030 | Production driver token scans |
| REQ-010 | Keep subbundle rows and gates meaningful. | All phases | Execution report and completed validator |
| REQ-011 | Avoid UI/mobile/browser proof for runtime/service-only changes. | All phases | No UI/media drift scan |
