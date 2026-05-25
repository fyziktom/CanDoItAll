# Risk To Solution Map

| Risk | Target solution |
| --- | --- |
| Architecture step implements product | Explicit operation contract + tool policy denies product mutation. |
| Work step creates architecture report but gets ProductMutation boundary | Artifact-production operation separated from product mutation. |
| Prompt aliases become writable | Process-boundary-aware alias grounding; read-only aliases cannot be promoted. |
| Recovery artifact rejected as wrong run | Recovery lineage fields and validation against recovery execution id. |
| Workflow completion blocks due no process artifact | Workflow artifact projection adapter before finalizer. |
| Subprocess parent uses stale child output | Source subprocess run id in parent projection and validation. |
| Downstream blocked forever | Materialization resolved event and unblock transition. |
| Negative branch hides missing own artifact | Disposition routing restricted to disposition steps and compatible failure types. |
| Malformed JSON passes | Storage-backed content parser. |
| Repeated bad evidence burns retries | Failure/artifact content fingerprint across attempts. |
| Active run adopted too early | Adopt only terminal runs or observe active runs with bounded waiting. |
| Linter ignored | Publish/start/readiness integration. |
