# B09 - AI agent profiles, provider bindings, capabilities, and governance

## Status

- `Completed`

## Objective

- Make AI agents a first-class party type with provider bindings, human ownership, capability records, validation status, and directory visibility.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance`
- Story IDs: AI-01, AI-02, AI-03, AI-04, AI-06, AI-07, AI-08, DIR-01, DIR-02

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ProviderExecution.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`

## Deliverables

- Ship the concrete outcome described by `B09` across route scope `/crm-hr/agents, /crm-hr/directory`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03`.
- Downstream dependents: `B10, B11, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical identity integration

## Implementation Steps

1. Reopen the preserved architect docs and the live repo source references listed above.
2. Re-run the entry gate against current code before editing feature files.
3. Implement only the smallest correct change set for this subbundle and its owned stories.
4. Run the proof required for this phase and update the execution report while the evidence is fresh.
5. Run the closure gate and reopen the subbundle immediately if proof is weak or contradicted by later behavior.

## Scope Exceptions

- None pre-approved. If current repo contracts force a scope change, repair the bundle before calling the phase complete.

## Execution Notes

- Completed on `2026-04-03` with the live repo contract rather than the stale bundle assumption that AI agents would also have a dedicated party role.
- Execution reality: actual agent identity is carried by `PartyType.AiAgent`, while human owners are tagged with `PartyRoleKind.AiSteward` when they become an agent steward.
- Provider binding reuses the existing Workspace provider registry and `ProviderProfile` storage instead of creating any CRM-HR duplicate.

## Recorded Proof

- `dotnet build src\CanDoItAll.Modules.CrmHr\CanDoItAll.Modules.CrmHr.csproj -v minimal`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter AiAgentsPageTests -v minimal`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter AiAgentProfileIntegrationTests -v minimal`
- `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter AiAgentFlowTests -v minimal`
- Screenshot review completed for:
  - `C:\repositories\CanDoItAll\evidence\crm-hr\b09\crm-hr-agents-b09-desktop.png`
  - `C:\repositories\CanDoItAll\evidence\crm-hr\b09\crm-hr-agents-b09-tablet.png`

## Do Not Do

- Do not import CanvasLib into CRM/HR pages.
- Do not bypass current storage-placement, search, activity, or project-structure service boundaries.
- Do not replace project-local participant behavior with a forced central-directory-only model.

## Acceptance Checklist

- An AI agent can be created as a party and linked to a provider profile.
- AI agent detail shows capabilities, owner, execution mode, and review state.
- The same AI agent can later be used by project integration flows.
- No duplicate provider registry is introduced.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr/agents, /crm-hr/directory`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b09\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B10, B11, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.

## Suggested Agent Prompt

```text
Implement B09 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B09_ai_agent_profiles_provider_bindings_and_governance against the live repo files listed under Exact Source References before editing code.
```

