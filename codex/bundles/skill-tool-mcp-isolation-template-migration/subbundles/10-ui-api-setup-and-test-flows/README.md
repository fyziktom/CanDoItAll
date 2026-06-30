# 10 UI API Setup And Test Flows

## Status

- `Completed`

## Objective

- Add Tool setup/edit/test UI and API flows, harden MCP setup with start/list-tools testing, and add typed capability access policy UI/API flows using the same services as runtime and templates.

## Success Criteria

- Users can create/edit Skill, Tool, and MCP capabilities without editing raw JSON for normal cases.
- Users can create/edit capability access restrictions for agents, processes, workflows, process steps, and workflow nodes without typing raw selector strings for normal cases.
- Users can test external tools and MCP servers during setup.
- MCP setup can show discovered tools and persist allowed tool decisions.
- UI/API setup responses preserve structured error category, correlation ID, masked detail, and repair hint.
- UI/API policy validation previews which assigned capabilities will be allowed or suppressed and why.

## Covered Inputs

- R04, R05, R06, R08, R09, R11, R13, R14, R15.

## Prerequisites

- SB07 template/seed hardening proof passes.
- SB08 MAF adapter/runtime proof passes.
- SB09 runtime hardening proof passes.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityConfigurationEditorSupport.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityConfigurationEditorSupport.cs`
- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inventories/03-error-state-inventory.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`

## Deliverables

- Tool setup wizard/editor state and validation.
- Capability access policy editor state, API DTOs, validation, and effective-set preview.
- API endpoints for capability setup tests, external tool test calls, and MCP list-tools inspection.
- UI result states for success, validation failure, startup failure, timeout, and masked secret-related errors.
- Typed API DTOs for setup-test results and failure categories; no string-only error contract.
- Component tests and Playwright e2e coverage.

## Dependency Impact

- SB11 depends on UI/API setup behavior for end-to-end regression proof.
- SB12 documentation depends on final setup flows.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Extend capability setup wizard to include Tool as a first-class kind.
2. Add typed editor support for internal/external tool configuration.
3. Add capability access policy editor support with catalog-backed capability key, tag, operation classification, MCP server, and MCP tool selectors.
4. Add effective capability preview API/service that returns allowed/suppressed descriptors and diagnostics without launching runtime execution.
5. Add MCP setup test action that starts/list-tools using SB04 service.
6. Add external tool setup test action using SB02 service.
7. Add API endpoints and DTOs for setup test results and policy validation/preview.
8. Show category-specific repair details while keeping raw diagnostic excerpts bounded, expandable, and masked.
9. Add component tests for validation and result states.
10. Add Playwright tests for Tool creation, MCP list-tools test, external tool dry run, failed MCP startup, failed external tool schema validation, and access policy deny/preview flows.

## Scope Exceptions

- Do not add unrelated UI redesign.
- Do not expose raw JSON as the primary setup path for Tool/MCP normal cases.
- Do not expose raw selector strings as the primary access policy authoring path.

## Do Not Do

- Do not run real user commands in component tests.
- Do not show unmasked secrets in UI or logs.
- Do not allow save to hide setup-test failures as success.
- Do not call process launchers or MCP lifecycle objects directly from Blazor components or API endpoints; use setup-test services.
- Do not duplicate policy evaluation rules in UI; call the shared evaluator/preview service.

## Acceptance Checklist

- "New tool" is available beside Skill and MCP setup.
- Capability access policy editor offers catalog-backed selectors for key, kind, tag, operation classification, MCP server, and MCP tool.
- Policy preview shows denied capabilities with rule scope, selector, reason, and repair hint.
- MCP test displays discovered tools and allowed-tool choices.
- External tool test displays deterministic fake result/error.
- Invalid schema or unsafe command shows actionable error before save.
- Existing Skill/MCP setup still works.
- Failed setup states show category, repair hint, correlation ID, and masked bounded detail.

## Proof Required

- API integration tests.
- Component tests for wizard/editor states.
- Component/API tests for policy editor validation and preview.
- Playwright screenshots and traces for setup flows.
- UI/API diagnostics assertions for failed external tool and failed MCP setup.
- `proof/SB10/manifest.md`
- `proof/SB10/semantic-invariants.md`

## Browser Validation Logging

- Target route/window: Agent Framework capability management surface.
- Required viewports: large desktop only for this execution; small and medium viewport passes intentionally skipped because the app targets large screens.
- Completed actions: opened the capabilities tab for a seeded agent, previewed a deny policy over an assigned Tool, opened the New Tool wizard, filled external-process setup fields, ran setup with malformed JSON, and verified the visible typed `JsonParse` diagnostic plus repair hint.
- Evidence paths: screenshot and command transcripts recorded in `proof/SB10/manifest.md`.
- Review questions: diagnostic content is visible, no raw secret display was introduced, and Tool/MCP/Skill counts remain readable on the large-screen route.

## Progression Gate

- SB11 is unblocked by `proof/SB10/manifest.md` and `proof/SB10/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement subbundle SB10 only. Add Tool setup, setup-test flows, capability access policy editing/preview, and harden MCP list-tools setup testing after SB09 passes. Use existing Radzen/project components where the app already uses them. Show structured, masked, repairable errors. Capture component and Playwright proof.
```

