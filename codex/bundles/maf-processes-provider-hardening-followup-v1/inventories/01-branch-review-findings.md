# Branch Review Findings

| ID | Finding | Severity | Evidence | Follow-up |
| --- | --- | --- | --- | --- |
| F-001 | Direct MAF -> Processes dependency removed for scoped process tools. | Good | `CanDoItAll.AgentFramework.Maf.csproj`; prior red-team report | Preserve with scans in SB01/SB12. |
| F-002 | New Tooling project is neutral and small. | Good | `CanDoItAll.AgentFramework.Tooling.csproj` | Harden descriptor metadata in SB02. |
| F-003 | MAF composes registered providers deterministically and validates duplicate tool names. | Good | `MafAgentRuntime.Capabilities.cs` | Refactor generic naming in SB03. |
| F-004 | Processes registers process runtime provider. | Good | `ProcessesModuleServiceCollectionExtensions.cs` | Preserve in SB07/SB08. |
| F-005 | MAF still has hard-coded project-structure attach path. | Medium | `MafAgentRuntime.Capabilities.cs` | SB04. |
| F-006 | MAF still has hard-coded image-generation attach path. | Medium | `MafAgentRuntime.Capabilities.cs` | SB05. |
| F-007 | Process provider is large and mixed-responsibility. | Medium | `ProcessAgentRuntimeToolProvider.cs` | SB07. |
| F-008 | Provider context purpose exists but is not yet a strong provider policy. | Medium | Tooling context + process provider | SB08. |
| F-009 | Large codex/bundles churn appears in branch diff. | Merge risk | compare branch diff | SB01. |
