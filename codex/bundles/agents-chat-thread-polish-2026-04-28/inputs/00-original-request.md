# Original Request

User feedback:

- The thread card must fit the left rail and must not be partially hidden under the chat window.
- The thread card preview is too long; show roughly half and place the remaining detail under TooltipService-backed help.
- The thread title must be editable with the shared `Editable.razor` component.
- The switch-agent modal must support search by name and filtering by tags using the shared `TagEditor.razor` component used in ZyphoNote-style tag search.
- Agent tags must not be added through the agent settings flow for this task.
- Users must be able to mark agents as favourite from the switch-agent modal with a standard star icon.
- Favourite agents are stored internally through a tag, are shown with a yellow star, and always sort first, including while searching.
