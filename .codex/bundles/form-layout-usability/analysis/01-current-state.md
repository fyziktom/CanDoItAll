# Current State

## Shared Form Foundation

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormField.razor` wraps label/content but the child field sits in an unqualified `flex-1` wrapper. The wrapper has no explicit `min-w-0` or `w-full`, so nested controls can shrink or fail to use full width in dense flex contexts.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TextArea.razor` defaults to `Rows = 4`, which is too small for note, prompt, instructions, JSON, and policy fields.
- `C:\repositories\CanDoItAll\Tailwind\forms\fields.css` gives `.cda-input--textarea` only line height. Raw `InputTextArea` usages that apply `cda-input cda-input--textarea` do not receive a stronger shared default min height.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormSection.razor` creates recognizable sections, but the chrome is card-heavy and lacks a compact icon/kicker affordance that helps scan enterprise settings forms.

## Product Form Hotspots

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessDefinitionForm.razor` has 17 `FormField` usages and 9 textareas. Governance and policy textareas are dense and should be split into topical tabs rather than one long vertical section.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor` has 15 `FormField` usages and 6 textareas. It is a dense step editor and likely benefits from topical grouping.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessRoleEditorForm.razor` has 11 `FormField` usages and 3 textareas.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` uses multiple editors; the secret editor screenshot shows usable structure but the payload and metadata fields can use stronger textarea defaults and clearer visual grouping.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\CandidatePipeline.razor`, `OpportunityEditor.razor`, `StaffingRequestEditor.razor`, `SkillMatrixPanel.razor`, and related CRM-HR editors have many form fields, several long textareas, and mixed raw `div`/label patterns.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor` already uses tabs, but relies on raw `InputTextArea` and module CSS for core textarea sizing.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor` and `PromptFactoryDialogs.razor` use custom `cw-textarea` / prompt editor styles; these should be validated by screenshot because they already aim for large text entry but may not stretch in floating inspectors.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` and project-structure dialog components include secret reference and process assignment forms with evidence screenshots already in the repo.

## Existing Screenshot Findings

- `project-structure-secret-dialog.png`: the modal has a wide empty right side while the create-secret form occupies only the left half. The value row mixes input and buttons, but the form does not use the modal width effectively.
- `secret-settings-revealed.png`: the settings secret editor uses a stronger split layout, but the payload section is low on the viewport and should preserve readable textarea defaults and full-width rows.
