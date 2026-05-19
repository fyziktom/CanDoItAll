# Assumptions And Risks

## Assumptions

- The workflow module's intended extension point is `Templates\Workflows\manifest.yaml` plus manifest-listed YAML files.
- The existing Gmail and Office365 summary examples should remain, while new task examples add a clearer "email to project tasks" path.
- Live plugin OAuth behavior is outside local proof unless configured accounts exist; template correctness can be proven by loader and graph validation.

## Risks

- A workflow that routes to `CreateTaskNodes` with an empty `tasks` array will fail. The email templates must route informational/no-action messages to summary asset branches instead.
- If processed-message marking follows project-structure creation, the project-structure step must preserve the prior LLM payload with `includeInputPayload: true` so message IDs remain available.
- If file-analysis templates rely on unsupported source extensions, source ingestion will reject code files. The template must configure allowed extensions explicitly.

## Critical Path Risks

- If the manifest does not load multiple files correctly, later template additions will be invisible. Subbundle 01 is a critical foundation.
- If seed version is not updated, already-managed workflows may not refresh with the new examples.
- If plugin executor IDs or JSON paths are wrong, email examples will load but fail at runtime.

## Validation Risks

- Unit tests can validate pack loading and graph construction, but cannot prove live Gmail/Office365 API calls without connected OAuth accounts.
- Browser proof is not required because the change is template data and loader tests rather than rendered UI behavior.

## Reopen Triggers

- Reopen subbundle 01 if loader validation cannot find new manifest-listed files or rejects duplicate keys.
- Reopen subbundle 02 if plugin executor payload shapes do not preserve `projectId`, `nodeId`, or processed-message IDs.
- Reopen subbundle 03 if source ingestion cannot read `.cs`, `.razor`, `.ts`, `.js`, `.json`, `.yaml`, `.md`, or `.txt` sources from the template settings.
