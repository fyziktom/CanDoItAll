# Templates

Repository-owned template assets live here. These files are runtime inputs for managed seed materialization and UI/API previews, not examples to copy into generated output.

## Structure

- `Agents/` - file-driven default agent template pack with team folders and per-member settings, skills, and instructions.
- `Capabilities/` - file-driven Skill, Tool, MCP, RAG, context, and capability access policy pack used by seed materialization.
- `Processes/` - file-driven process template pack used by the Processes module.
- `Workflows/` - file-driven workflow example template pack used by the Agent Framework module.

Add future template sets as sibling module folders under `Templates/`. Do not place template packs in generated, ignored, or runtime output directories.

## Capability Pack

The default capability pack is rooted at `Templates/Capabilities/manifest.json`. The manifest lists capability files (`skills.json`, `tools.json`, `mcps.json`, `other.json`) and policy files (`policies/capability-access-policy.json`). `SandboxWorkspaceSeedBuilder` loads this pack through `CapabilityTemplatePackLoader` and creates catalog rows only through `CapabilityTemplateSeedMaterializer`; do not add new hardcoded `CapabilityCatalogItem` builders to the seed builder.

Every capability descriptor must have:

- `kind`: `skill`, `tool`, `mcp-server`, `rag`, `ai-context`, `memory`, or another supported enum value.
- `key`: stable lower kebab-case catalog key.
- `displayName` and `description`: user-facing catalog text.
- `stableId` and `stableGuidKey`: stable identity inputs for deterministic seed IDs.
- `operationClassifications`: typed policy classifications such as `read`, `write`, `mutation`, `validation`, `script-execution`, `external-action`, `browser-access`, `mcp-tool`, `provider-native`, or `runtime-launch`.
- `proofNotes`: short current proof state when setup has not been live-tested.

## Adding Skills

Internal-agent skills are app templates. Store their instructions under `Templates/Capabilities/skills/instructions/` and reusable resources under `Templates/Capabilities/skills/resources/`. Do not point managed app capabilities at `~/.codex/skills`; those are Codex development skills for building CanDoItAll, not runtime template inputs for internal agents.

Template skills must stay generic to the capability they describe. They may guide .NET delivery, Blazor validation, Playwright proof, code analytics, components, mail, documents, or spreadsheets, but they must not contain one-off process-run instructions, task-specific generated-app content, or Codex bundle workflow guidance. Processes compose these skills with step briefs, allowed operations, artifact expectations, and launch variables at runtime.

For an inline skill, use `skillSource: "inline"` and an `inlineSkill` block with `name`, `description`, `instructionsAssetKey`, and optional `resources`. `instructionsAssetKey` and resource `contentAssetKey` values are repository-relative paths under `Templates/Capabilities`, for example `skills/instructions/dotnet-app-delivery.md` or `skills/resources/dotnet-command-examples.md`. The capability template loader validates these files before seeding.

File skills are reserved for explicitly user-provided workspace skills. Do not use file skills for the default app-owned capability pack unless the skill root also lives under the app template tree and the ownership boundary is reviewed.

For a registered skill, bind through an implementation key and service registration in the skill implementation project. The catalog template owns the capability key and user-facing metadata; the implementation project owns execution.

## Adding Tools

For an internal workspace/runtime tool, add a `tool` descriptor to `tools.json` with `runtimeToolName` in snake_case and matching operation classifications. The runtime tool name must map to `ToolContractCatalog` or `ToolCapabilityRegistry` metadata, and MAF attachment must continue to expose it through typed `ToolDescriptor`/`CapabilityExposureDescriptor` conversion.

For an external process or HTTP tool, define the descriptor and setup requirements in the capability template/descriptor layer, then execute setup through `IToolSetupTestService`. External setup failures must return `CapabilityDiagnostic` entries with category, field path, transport, correlation ID, bounded masked detail, and repair hint. Do not collapse process, HTTP, JSON, schema, timeout, cancellation, or command-policy failures into a generic setup error.

If a tool is provided by a plugin or runtime provider, the provider must expose unique tool names and metadata. Runtime provider pruning must go through `ICapabilityAccessPolicyEvaluator` and record suppression diagnostics; do not add provider-specific hidden deny lists.

## Adding MCP Servers

Add MCP servers to `mcps.json` with `kind: "mcp-server"`, a stable `mcpServerKey`, operation classifications, and an `mcpTransport` block.

For local stdio MCP servers, declare `transport: "local-stdio"`, `command`, `arguments`, and non-empty `allowedTools`. Local commands are checked by command policy, and raw environment values are rejected; use secret bindings instead.

For remote HTTP MCP servers, declare the endpoint and header bindings. Raw headers are rejected. Setup runs through `IMcpSetupTestService`, validates startup/list-tools/allowed-tools/cleanup, and emits typed `CapabilityDiagnostic` values. MCP tool selectors must include both server key and tool name.

`Playwright Local MCP` is the default browser-proof MCP capability. Its descriptor lives in `mcps.json`, starts through `npx @playwright/mcp@latest`, and exposes only the configured allowed browser tools to agents after runtime capability filtering. Validate it through the capability setup UI/API before relying on it in a process or workflow.

## Access Policies

Capability restrictions for agents, processes, workflows, runtime overrides, and UI previews use typed `CapabilityAccessPolicy` rules. Normal editing should happen through the typed UI/API, not hand-edited raw JSON.

Supported selector kinds are:

- `all`
- `kind`
- `capabilityKey`
- `tag`
- `operationClassification`
- `runtimeToolName`
- `mcpServerKey`
- `mcpToolName`
- `implementationKey`

Use `deny` rules for suppression and `require` rules for required availability diagnostics. `allow` rules do not grant capabilities that are not already assigned or available. Required capability denial must include the rule ID, scope, selector kind, capability identity, correlation ID, and repair hint.

Process `allowedOperations` are compiled into typed operation-classification policies through `ProcessAllowedOperationsCapabilityPolicyCompiler`. Agent workspace-tool settings are compiled through `AgentWorkspaceToolAccessCapabilityPolicyCompiler`. MAF composition consumes the resulting `EffectiveCapabilitySet`; do not reintroduce raw selector string comparisons or private runtime filters.

## Setup And Repair Flow

Use the capability setup UI/API to test external tools and MCP servers before enabling them. Representative failures should be interpreted from the diagnostic fields:

- `JsonParse` at `$.jsonInput` or `$`: fix malformed setup input or non-JSON tool output.
- `CommandPolicy` at `$.command` or `$.executablePath`: choose an approved command/executable.
- `SecretBinding`: replace raw secret values with bindings.
- `McpListTools` or `$.allowedTools`: update MCP allowed tools to match discovery or repair the server.
- `Timeout` or `Cancellation`: check lifecycle ownership and timeout before retrying.
- `ResourceCleanup`: inspect MCP shutdown cleanup before enabling the descriptor.

Diagnostics shown in UI/API must keep raw detail bounded and masked. Logs and stored proof should never contain raw API keys, bearer tokens, authorization headers, or raw environment secrets.

## Managed Seed Versioning

When capability templates change default behavior, update the capability pack manifest version/seed version deliberately and keep compatibility with existing persisted catalogs. Existing persisted catalog rows may require compatibility adapters, but new default seed content must be template-backed. Guard tests under `CapabilityMigrationCleanupGuardTests` prevent reintroducing hardcoded default capability builders, private MAF capability descriptor DTOs, hidden suppression outside the shared evaluator, raw selector string matching in runtime access logic, and generic setup errors.
