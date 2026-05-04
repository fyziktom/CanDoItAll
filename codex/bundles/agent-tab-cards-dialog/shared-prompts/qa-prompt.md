# QA Prompt

Validate the implemented subbundle against the original request, not only the code diff.

- Confirm the switch-agent modal and Agents tab share the same card component.
- Confirm double-click on an Agents tab card opens a DialogService dialog.
- Confirm the dialog shows tabs for Identity, Runtime, Project Structure Access, Workspace Tools, Process Access, Skills and MCP, and Tags.
- Confirm Summary and Instructions are full-width in Identity and have larger default height.
- Confirm save/delete and capability assign/remove still persist through workspace services.
- For browser proof, inspect `/agents?tab=agents` on a large viewport and a narrower viewport, then inspect the open dialog state for readability, clipping, available-space use, tab usability, and z-order.
