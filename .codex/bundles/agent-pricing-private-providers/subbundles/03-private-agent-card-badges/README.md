# 03 Private Agent Card Badges

## Status

- `Completed`

## Objective

Show a `Private` badge on shared agent card surfaces when the agent uses a private-style provider.

## Covered Inputs

- User note: each agent that uses a private-style provider must show `Private` where the agent is displayed as a card.

## Prerequisites

- SB01 exposes private-provider metadata from provider records.
- Agent card callers can resolve provider profile IDs for their agents.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Components/AgentSelectionCard.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor`
- `repo://src/CanDoItAll.AgentFramework.Components/AgentSwitchDialog.razor`

## Deliverables

- `AgentSelectionCard` accepts a typed private-provider indicator.
- Catalog and switcher card callers pass private-provider state derived from provider metadata.
- Badge styling uses the existing badge/component system.

## Dependency Impact

- Agent catalog and switch dialog rendering gain one additional badge in the existing badge cluster.
- Provider loading in callers must remain predictable and avoid hidden lifecycle side effects.

## Validation Depth

- Component or source assertion proof for catalog/switch card badge wiring.
- Browser validation of `/agents?tab=agents` if the local app starts within the available window.

## Implementation Steps

1. Add a private-provider parameter to the shared card.
2. Render `Private` through the existing badge UI.
3. Load or derive private-provider sets in card-owning surfaces.
4. Add focused component test coverage where practical.

## Do Not Do

- Do not duplicate card markup in caller components.
- Do not infer privacy from display names or model strings.

## Acceptance Checklist

- Ollama/private-backed agents render a `Private` badge.
- OpenAI-backed agents do not render the badge unless explicitly marked private.
- The badge is available in catalog and switcher card views.

## Proof Required

- Passing component test or source proof for private badge rendering.
- Browser validation log or documented startup blocker.

## Browser Validation Logging

- Record route `/agents?tab=agents`, viewport, screenshot path if browser proof succeeds, or exact blocker if it does not.

## Progression Gate

- Final closure cannot pass while the shared card lacks a provider-derived private indicator.

## Suggested Agent Prompt

Use the shared implementation prompt and implement only private-provider card badge wiring.
