# Agent, Tool, Skill, And MCP Evidence

## Agents Observed In The Successful Process Run

The successful Tetris process run involved these roles/agents:

| Agent | Role in run | Notes |
| --- | --- | --- |
| `Product owner AI agent` | Scope and release boundary | CRM/HR-provisioned project context role; used for intake/scope. |
| `.NET Solution Architect` | Architecture and canonical-model impact | Template key `dotnet-solution-architect`; workspace profile `architecture-review`; read/write files and transform artifacts. |
| `Blazor Application Developer` | Implementation and peer review | Template key `blazor-application-developer`; workspace profile `software-development`; can read/write files, run validation, run scripts, scaffold projects, manage workspace paths, and transform artifacts. |
| `JavaScript QA Review Lead` | Browser/runtime QA validation | Template key `javascript-qa-review-lead`; workspace profile `quality-validation`; can run validation and local scripts; browser proof discipline was important. |
| `Security Reviewer` | Security/data handling review | Template key `security-reviewer`; workspace profile `security-review`; validation allowed, local scripts disabled. |
| `Delivery Manager` | Release approval and post-release learning | Template key `delivery-manager`; business-analysis profile; can write storage/project-structure style artifacts. |
| `Release Readiness Manager` | Controlled rollout | Template key `release-readiness-manager`; quality-validation profile; validation/local scripts allowed. |

Raw agent catalog evidence:

- `inputs/api-captures/agents-include-templates.json`
- `inputs/api-captures/agent-execution-runs-for-process-6724.json`

## Providers

Provider catalog evidence is saved in `inputs/api-captures/agent-providers.json`.

Important provider observations:

- `OpenAI default`
  - Responses transport.
  - Default model observed: `gpt-5.4-mini`.
  - Supports streaming, tools, background responses, and pricing metadata.
  - API key is represented through secret reference fields, not exposed as plaintext in the captured JSON.

- `OpenAI chat completions`
  - Chat-completions style provider.

- `OpenAI image generation`
  - Image-generation provider.

- `Local Ollama`
  - Local provider option.

- `Remote Ollama`
  - Fallback provider option exists in runtime code and catalog behavior.

Hardening input:

The existence of runtime fallback provider behavior should be reviewed against the user principle that fallback mechanisms must not silently hide errors. Not every fallback is wrong, but production-like dispatch should make provider substitution explicit and diagnosable.

## Capabilities And Tools

Capability catalog evidence:

- `inputs/api-captures/agent-capabilities.json`

Notable capability/tool families observed:

- Playwright local MCP.
- CanDoItAll code analytics MCP.
- CanDoItAll components MCP.
- CanDoItAll frontend/theme skills.
- Concrete deliverable delivery inline skill.
- .NET app delivery inline skill.
- Workspace file tools:
  - `workspace_read_file`
  - `workspace_write_file`
  - `workspace_append_file`
  - `workspace_create_directory`
  - `workspace_list_files`
  - `workspace_search`
  - `workspace_stat_path`
  - `workspace_diff_text`
- Workspace command tools:
  - `workspace_dotnet_restore`
  - `workspace_dotnet_build`
  - `workspace_dotnet_test`
  - `workspace_dotnet_run`
  - `workspace_dotnet_new`
  - `workspace_pwsh_run_script`
  - `workspace_python_run_file`
- Git/status tools exposed through the agent runtime.
- Storage/source RAG capabilities.

The Playwright local MCP descriptor included command shape similar to:

```text
npx @playwright/mcp@latest --headless --caps vision --ignore-https-errors --isolated
```

Browser tool names observed in code/catalog:

- `browser_navigate`
- `browser_snapshot`
- `browser_console_messages`
- `browser_take_screenshot`
- `browser_click`
- `browser_type`
- `browser_fill`
- other Playwright interaction helpers

## Workflow Executors

Executor catalog evidence:

- `inputs/api-captures/workflow-executor-catalog.json`
- `inputs/api-captures/workflow-runtime-backends.json`

Executor families observed:

- `storage.file`
- `project-structure`
- `http.fetch`
- `image.generate`
- `spreadsheet`
- `gmail.mark-message-processed`
- `gmail.messages-by-label`
- `json.transform`
- `office365.mark-message-processed`
- `office365.messages-by-category`
- `office365.message-by-address-unprocessed`
- `source.ingest`
- `markdown.render`
- `human.approval`
- `utility.delay`
- `command.process`

Important availability distinction:

- Office365 executors used by the live workflow were executable.
- Gmail executors appeared in the catalog but were described as not executable in this environment.
- Some planned executors are catalog-visible but unavailable. The later bundle should verify that unavailable executors cannot be selected/executed without clear diagnostic state.

## Skills Used During This Preparation

User explicitly asked to use the CanDoItAll bundle workflow skill. This preparation read and followed the input-preparation portions of:

- `candoitall-bundle-workflow`
- `candoitall-bundle-preparation`
- `candoitall-api-processes`
- `candoitall-api-agents`
- `candoitall-api-workflows`

This bundle intentionally stops before architecture or subbundle preparation because the user explicitly requested input information only.

## Skill/Tool Trouble Evidence From Current Changes

The repository delta since the baseline shows repeated hardening of agent instructions and API skills around these trouble classes:

- agents using stale examples or sibling external-target projects
- agents creating test projects outside the grounded product root
- agents retrying failed tool calls without reading diagnostics
- agents treating `workspace_dotnet_new` parent paths as product roots
- agents using wrong timeout units
- agents leaving `workspace_dotnet_run` hosts alive and locking later builds
- agents claiming browser proof from chat-only or stale artifacts
- agents using browser/static-server helpers that block forever
- agents relying on direct MCP assumptions where HTTP APIs are now canonical

Live preparation also observed a host/build-lock variant of this class:

- Existing `CanDoItAll.Web.exe` on 5034 locked build outputs.
- The requested 5032/dev host needed to be started from existing build output rather than through a fresh `dotnet run`.

## Cross-Surface Canonicity Inputs

The same concepts appear across several layers:

- process template JSON
- process template sidecar markdown
- process template baseline scenarios
- process API numeric enums
- runtime policy classes
- dispatch prompt builders
- agent instructions
- agent capability catalog
- tool invocation policy
- workflow executor catalog
- workflow canvas UI
- project-structure writeback
- API skills
- tests

Examples of concepts that need one canonical source or at least consistent enforcement:

- process allowed operations
- operation target scope
- required artifact satisfaction status
- artifact projection lineage
- browser proof requirements
- runtime command keep-alive/lifetime semantics
- external-target alias boundaries
- workflow executor availability
- capability proof status
- provider substitution/fallback behavior
- enum numeric/string presentation over HTTP
