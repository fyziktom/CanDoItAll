# Source Artifacts

## User Input

- `inputs/00-original-request.md`

## Existing Screenshot Artifacts

- `C:\repositories\CanDoItAll\project-structure-secret-dialog.png`
- `C:\repositories\CanDoItAll\secret-settings-revealed.png`

## Repo Discovery Commands

- `rg -n '<FormField|<FormRow|<FormSection|<TextArea|InputTextArea|<textarea|<EditForm|RadzenTextArea|RadzenTemplateForm' src -g '*.razor'`
- `rg -n '@page ' src -g '*.razor'`
- PowerShell form count inventory over `src/**/*.razor`

## Tooling Notes

- `candoitall_components` MCP was attempted before custom structural work and failed with `Transport closed`.
- `npx` is available at `C:\Program Files\nodejs\npx.cmd`; Playwright CLI validation can run through the local skill wrapper.
