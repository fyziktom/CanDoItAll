# Scope Inventory

## Source Scan Summary

The scan searched `src/**/*.razor` for `FormField`, `FormRow`, `FormSection`, `TextArea`, `InputTextArea`, raw `textarea`, `EditForm`, and Radzen form components.

## Highest-Density Product Form Files

| File | Form Signals | Notes |
| --- | ---: | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessDefinitionForm.razor` | 26 | Critical dense process editor, many policy textareas. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor` | 21 | Dense step editor with multiple long fields. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\CandidatePipeline.razor` | 18 | Recruiting form with candidate, stage, and notes fields. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessRoleEditorForm.razor` | 14 | Role editor with responsibilities and permissions text. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` | 12 | Project-structure dialogs and secret reference forms. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor` | 12 | Hiring conversion editor with many fields. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\SkillMatrixPanel.razor` | 11 | Skill and certification forms. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\OnboardingChecklistPanel.razor` | 11 | Lifecycle task forms. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsOperatorConsoleSection.razor` | 10 | Runtime operation notes/messages. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\StaffingRequestEditor.razor` | 10 | Staffing request fields and notes. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectModalHost.razor` | 9 | Project create/edit modal fields. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\OpportunityEditor.razor` | 5 textareas | Long opportunity content fields but raw label patterns. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor` | 5 textareas | Already tabbed; textarea defaults still relevant. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor` | 8 textareas | Custom prompt/editor textareas and floating inspector. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` | 3 textareas, multiple forms | Workspace, secrets, providers, API token forms. |

## Shared Component Files

| File | Role |
| --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormField.razor` | Shared label/content wrapper for form fields. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormRow.razor` | Shared row/grid wrapper for paired fields. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormSection.razor` | Shared section wrapper for grouped form content. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TextArea.razor` | Shared textarea wrapper. |
| `C:\repositories\CanDoItAll\Tailwind\forms\fields.css` | Shared form field/input styles. |

## Screenshot Targets

Initial target screenshots:

- Project structure secret reference dialog from existing artifact.
- Workspace settings secret editor from existing artifact plus live route.
- Process definition form.
- Process step editor form.
- CRM-HR opportunity or candidate editor.
- Agent details dialog identity/instructions tab.
- Prompt factory long text editor or component editor.
- Shared input sandbox.
