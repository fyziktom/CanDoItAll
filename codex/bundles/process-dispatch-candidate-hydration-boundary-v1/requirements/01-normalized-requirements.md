# Normalized Requirements

| ID | Requirement | Owning subbundles | Proof |
| --- | --- | --- | --- |
| RQ-001 | Preserve all behavior from the completed claim/route bundle. | SB01, SB04, SB08, SB12, SB16, SB18 | Previous bundle smoke, focused dispatch tests, full build. |
| RQ-002 | Keep all work module-local under `CanDoItAll.Modules.Processes`. | All | Architecture source scans. |
| RQ-003 | Do not introduce `CanDoItAll.Processes.Core`, process driver packs, driver registry, or production driver APIs. | All gates | No-core/no-driver scans. |
| RQ-004 | Inventory candidate header selection and candidate hydration from live source before production movement. | SB02 | Source inventory and line/method map. |
| RQ-005 | Add or extend architecture guardrails before moving production behavior. | SB04 | Unit architecture tests. |
| RQ-006 | Isolate candidate header selection into a module-local query/selector helper while preserving lease/status/run eligibility semantics. | SB05-SB06 | Header selection parity tests. |
| RQ-007 | Introduce candidate hydration snapshots that capture run, definition, step, artifacts, branch outcomes, assignments, and expected inputs without exposing EF entities outside the module. | SB07 | Build and snapshot tests. |
| RQ-008 | Split artifact-input preparation from the main hydration method while preserving prompt shaping and managed path behavior. | SB09 | Artifact-input parity tests. |
| RQ-009 | Split branch outcome and conditional dependency shaping from the main hydration method. | SB10 | Branch outcome parity tests. |
| RQ-010 | Split workflow assignment recognition and current assignment/role resolution from the main hydration method. | SB11 | Workflow route parity tests. |
| RQ-011 | Create a module-local technical-agent binding/access preparation boundary without hiding its side effects. | SB13-SB14 | Binding/access mutation tests and source scans. |
| RQ-012 | Preserve manual recovery directive and artifact recovery execution selection behavior. | SB15 | Recovery/hydration parity tests. |
| RQ-013 | Add driver-readiness documentation for candidate/evidence intents without adding production driver APIs. | SB17 | Documentation and no-driver scan. |
| RQ-014 | Keep browser proof N/A unless UI changes unexpectedly; no small/medium/mobile proof artifacts. | All | Proof-path scan. |
| RQ-015 | Enforce refactor gates after every few subbundles. | SB04, SB08, SB12, SB16, SB18 | Gate transcripts and execution report sections. |
