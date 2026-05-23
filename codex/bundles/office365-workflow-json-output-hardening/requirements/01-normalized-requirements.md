# Normalized Requirements

| Requirement | Source | Acceptance Criteria | Owning Subbundle |
| --- | --- | --- | --- |
| R1 JSON response format enforcement | N001 | JSON-required workflow LLM components pass an explicit JSON response format, using the component response schema when present. | SB01 |
| R2 Strict failure retained | N001 | Malformed model output still throws `InvalidOperationException` from `ValidateJsonPayload`; no JSON extraction, code-fence stripping, or repair fallback is introduced. | SB01 |
| R3 Provider capability clarity | N001 | A JSON-required workflow component fails before the provider call when the selected provider cannot support structured/JSON response format enforcement. | SB01 |
| R4 Office365 context preservation | N003, N004 | `projectId`, `nodeId`, `project`, and `runContext.office365Processing` remain available to downstream project-structure and Office365 processed-category executors. | SB01, SB02 |
| R5 Live Office365 validation | N002, N004 | The local running app or API is used to run or inspect the Office365 summary workflow against the available categorized email; if blocked, exact blocker and alternative proof are recorded. | SB02 |

## Scope Exceptions

- This bundle does not redesign all workflow template schemas.
- This bundle does not add silent retry or model-output repair. That would hide a contract violation and conflict with strict workflow validation.
