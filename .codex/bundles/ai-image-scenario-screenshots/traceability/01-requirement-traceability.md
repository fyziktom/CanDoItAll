# Requirement Traceability

| Raw note | Normalized requirements | Owning subbundle | Source files/artifacts | Planned proof |
| --- | --- | --- | --- | --- |
| Add providers profiles for image generation AIs, default OpenAI, later ComfyUI. | R1, R3 | `01-image-provider-profile-foundation` | `ProviderModels.cs`, `Enums.cs`, `SandboxWorkspaceSeedBuilder.cs` | Build/tests; provider catalog/API readback. |
| Agents must have image generation as default tool if allowed and preferred provider per agent. | R2, R8, R9 | `01-image-provider-profile-foundation`, `04-screenshot-agent-template-and-asset-storage` | `AgentModels.cs`, `AgentWorkspaceToolAccessModels.cs`, seed assets | Metadata serialization tests; agent catalog readback. |
| Add projects for each scenario app and project-structure nodes. | R4, R5 | `02-scenario-project-structure-seeding` | `run-manifest.json`, scenario roots, `ProjectStructureAgentApi.cs` | Project and structure readback. |
| Add single-page and multiple-page screenshot templates. | R6, R7 | `03-screenshot-process-template-pack` | `Templates\Processes\manifest.json`, existing template definitions | Template pack validation; API template detail/import. |
| Add screenshot capture and review/storage agent templates. | R8, R9 | `04-screenshot-agent-template-and-asset-storage` | `SandboxWorkspaceSeedBuilder.cs`, seed instruction assets, Playwright MCP capability | Agent readback; capability verification; asset API proof. |
| Add process node under delivery block and run first app screenshot process. | R10, R11 | `05-first-scenario-runtime-proof` | Scenario 01 source root, process/project APIs | Run detail; Playwright screenshot; asset node/content readback. |
| Add process and agent for AI layout recommendations from screenshots. | R12 | `06-layout-image-generation-workflow` | Screenshot assets, image provider profile, process templates | Layout process run; generated image asset readback or provider blocker. |
