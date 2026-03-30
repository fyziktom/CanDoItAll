# QA Prompt

Validate the current subbundle at the required depth.

Minimum checks:

- Run the documented build or test commands.
- If the subbundle changes shared assets or shared components, open real browser routes and verify the surface renders correctly.
- Confirm the duplicate or folder-density claim with an explicit audit command, not by visual impression.
- Review screenshots for missing assets, clipped overlays, broken shell spacing, and route load failures.
- Reopen the subbundle if proof is incomplete or if later work exposes a weak earlier assumption.
