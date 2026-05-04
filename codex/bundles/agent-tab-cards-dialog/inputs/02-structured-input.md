# Structured Input

## Raw Notes

| Id | Exact wording | Requirement signal |
| --- | --- | --- |
| N001 | "change layout in agent page tab Agents" | The `/agents?tab=agents` tab must change visibly. |
| N002 | "Hero of that tab will be Agents cards similar as we have in chat tab in switch agent modal. Ideal is to use same component for both." | Agents tab and switch-agent modal should share one card component. |
| N003 | "when I doubleclick on some of those agents in agents tab, it opens modal (dialog service)" | Agent card double-click on Agents tab must open a DialogService modal. |
| N004 | "that shows agent details (what we have now in \"technical editor\" on agents tab and allow setting." | Current technical editor capability must remain editable in the modal. |
| N005 | "modal must have it in tabs. Identity, Runtime, Project Structure Access, etc will be each on own tab in dialog." | Editor sections must become modal tabs. |
| N006 | "It must have also tab for showing connected skills and mcps servers with possibility to assign new (or from available list)." | Modal needs a capabilities tab for attached skills/MCP servers and available assignment. |
| N007 | "Assure that fields for editing agent parameters will be correctly using available space." | Text inputs/layout must fill available width. |
| N008 | "Summary and Instructions in Identity part of agent config are not stretched to whole width of the available space and it should have larger default height." | Summary and Instructions text areas need full width and larger default heights. |

## Constraints

- Use the requested CanDoItAll bundle workflow.
- Keep the Blazor implementation aligned with existing BaseLib components.
- Preserve existing save/delete/project/process/workspace capability behavior.
- Use DialogService for the details modal.
- Include browser proof for the UI change unless the local app cannot be started.
