# Normalized Requirements

| ID | Requirement | Acceptance criteria |
| --- | --- | --- |
| RQ-001 | Preserve all existing behavior | No original process runtime behavior, artifact satisfaction branch, projection path, validation status, step transition, recovery rule, or diagnostic is removed or weakened. |
| RQ-002 | No Process Core extraction | Do not create `CanDoItAll.Processes.Core`, move EF entities, or promote private dispatcher models into public contracts in this bundle. |
| RQ-003 | No production process-driver API | Do not introduce `IProcessDriverPack`, driver registries, driver packages, or production driver contracts; driver work is documentation-only readiness mapping. |
| RQ-004 | Module-local helper boundaries | All new helpers remain under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/` and do not reference MAF adapter, UI/Razor, new Core projects, or external driver APIs. |
| RQ-005 | Artifact satisfaction isolation | Extract required-artifact satisfaction, auto-satisfaction, response text, provider-native browser, external-target, shallow path, and quality validation helpers gradually. |
| RQ-006 | Side-effect clarity | Pure helpers do not perform file, directory, storage, DbContext, service-scope, transition, or agent mutation side effects. |
| RQ-007 | Future driver readiness | Maintain documentation-only evidence-family vocabulary for future drivers without coupling current runtime to drivers. |
| RQ-008 | Long phased execution | Use many small subbundles with critical gates every few steps so Codex cannot finish with shallow naming-only extraction. |
| RQ-009 | No small/medium/mobile proof | Runtime/service refactor records browser validation as `N/A`; if UI changes unexpectedly, only large desktop/PC proof is allowed. |

