# Assumptions And Risks

## Assumptions

- Workflow executor nodes should be represented as a generic node kind plus a typed executor id/settings payload, not as one enum value per tool. This keeps plugin extension viable.
- In-process preview execution is sufficient for this bundle's implemented proof; durable production hosting remains a follow-up unless the app already has host infrastructure available.
- Project-structure executor implementation can use existing project-structure services from the MAF project, because that project already references Workbench and related modules.
- Image generation executor can initially call the existing provider/tool service path; if no configured image provider exists, the provider test must record a blocked result instead of faking success.

## Critical Path Risks

- Subbundle 01 is a critical foundation: weak executor contracts would make plugin UI setup and durable execution hard to evolve.
- Subbundle 02 is a critical foundation: ClosedXML leakage outside `CanDoItAll.Tools.Documents` would violate the user's explicit architecture constraint.
- Subbundle 04 is a critical foundation: if MAF compilation still binds executor nodes as pass-through delegates, all downstream UI and tests are false confidence.
- If workflow node settings are stored as unvalidated JSON without descriptor-backed validation, bad definitions will fail at runtime instead of at authoring/save time.

## Validation Risks

- `gpt-5-mini` validation may be blocked by missing OpenAI credentials or absent provider profile; record exact provider state and failure rather than skipping.
- `gptoss20b64k` validation may be blocked by Ollama service/model availability; run a real `ollama` check and record command output summary.
- Browser proof may require the local web app/dotnet-watch stack to start cleanly; if startup fails, record the exact build/startup blocker.
- Project-structure executor scenarios require a project with known nodes/assets; seed or use existing scenario data, then record ids used.

## Reopen Triggers

- Reopen subbundle 01 if any built-in executor needs settings that cannot be represented by the contract.
- Reopen subbundle 02 if spreadsheet scenarios require ClosedXML features not exposed by the wrapper.
- Reopen subbundle 04 if tests show node output artifacts/events are not persisted or mapped to workflow node ids.
- Reopen subbundle 05 if right-click actions can create executor nodes but the inspector cannot configure required settings.
- Reopen subbundle 06 if any of the 20 scenarios only proves catalog creation rather than actual workflow execution.
