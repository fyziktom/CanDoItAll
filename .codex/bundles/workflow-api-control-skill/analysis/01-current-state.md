# Current State

## Workflow API Surface

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs` already exposes workflow settings, runtime backends, executor catalog, definitions list/get/save/delete/validate, draft validation, provider options, components list/get/save/delete, test runs, saved run start, run list/page/detail/cancel, events/page, artifacts, pending external requests, external request response, and analytics.
- The existing API is typed around `WorkflowId`, `WorkflowVersionId`, `WorkflowRunId`, `WorkflowLifecycleStatus`, `WorkflowRuntimeBackendKind`, and workflow DTO records. There is no generic string command dispatch.
- `tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs` proves save/validate/test-run, route metadata round trip, validation failures, backend failure, OpenAPI exposure, and provider options.

## Compared Process API Surface

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs` has explicit process definition publish, export, and import commands in addition to save/delete.
- Workflow definitions have a lifecycle enum with `Draft`, `Active`, `Suspended`, and `Archived`, but the API requires callers to reconstruct and resubmit a whole `WorkflowDefinitionSaveRequest` to change status.
- Workflow definitions have no import/export command, while processes and agents do. That is a real development-control gap because saved workflow graphs are JSON-heavy and should be portable without hand-copying graph payloads.

## Skill And Install Surface

- `C:\repositories\CanDoItAll\codex\skills\candoitall-api-project-structure\SKILL.md` and `C:\repositories\CanDoItAll\codex\skills\candoitall-api-processes\SKILL.md` are short instruction-only skills with required frontmatter, API access guidance, route inventory, operating rules, and validation guidance.
- `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1` and `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1` both discover repo-managed skills by scanning `codex\skills` recursively for `SKILL.md`, so a new skill folder is picked up without a hard-coded list.
- Official OpenAI Codex skill docs say a skill is a directory containing `SKILL.md`, that `name` and `description` are required, and that concise descriptions drive implicit invocation. The new skill should stay focused and avoid unnecessary scripts.

## Tooling Observations

- `rg.exe` and `codex.exe` from the Windows app package failed with access denied in this environment, so searches used `git ls-files`, `Select-String`, and direct PowerShell reads.
- OpenAI docs MCP was not exposed by tool discovery; official OpenAI web docs were used as the fallback source.
