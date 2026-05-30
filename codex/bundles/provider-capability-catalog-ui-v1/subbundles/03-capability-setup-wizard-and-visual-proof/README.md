# Capability setup wizard and visual proof

## Status

- `Completed`

## Objective

- Add the MCP/Skill setup wizard based on generated visual proposals and ASCII layouts, then prove the final UI visually and structurally.

## Covered Inputs

- N07, N08, N10, N12.

## Prerequisites

- SB01 and SB02 closure gates passed.
- Imagegen proposals and ASCII layouts recorded in architecture.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor.cs`
- `repo://src/CanDoItAll.AppComponents/Components/Steps.razor`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/StorageSettingsPanel.razor`

## Deliverables

- Wizard dialog for MCP server and Skill setup.
- `InputFile`-based `SKILL.md` upload path for inline skill draft.
- Validation/review summary before save.
- Browser proof and final closure.

## Dependency Impact

- This is the final user-visible creation flow; weak wizard proof leaves capability management incomplete.

## Validation Depth

- Critical UI closure.

## Implementation Steps

1. Implement wizard model and typed configuration builders.
2. Add `Steps` layout matching ASCII layouts.
3. Add upload handling for `SKILL.md`.
4. Wire wizard launch buttons from capabilities panel.
5. Add tests for wizard save outputs.
6. Run browser proof and closure validators.

## Scope Exceptions

- Does not implement `/skills-tag:*` runtime prompt shortcut.
- Does not guarantee arbitrary uploaded skill script execution; upload is catalog metadata/instructions setup.

## Do Not Do

- Do not persist raw secrets in MCP configuration.
- Do not bypass existing capability save service.

## Acceptance Checklist

- New MCP wizard output has command/arguments/allowed tools JSON.
- New Skill wizard output has either file skill root or inline skill instructions.
- New capability appears in inventory after save.
- Wizard screenshots match proposal structure closely enough: left steps, center form, right review.

## Proof Required

- Targeted component tests for wizard outputs.
- Browser screenshots for Type, MCP configuration, Skill configuration, and Review steps.
- Completed-stage bundle validator.
- Closure manifest: `bundle://proof/SB03/manifest.md`.
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`.

## Browser Validation Logging

- Route: `/agents?tab=capabilities`.
- Viewport: large desktop.
- Actions: open wizard, switch MCP/Skill, advance steps, verify review panel, save one capability.
- Screenshot review: no hidden footer buttons, no clipped upload area, no dialog overlay clipping.

## Progression Gate

- Final closure requires tests/build/browser evidence, raw-note closure, and completed-stage validator.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
