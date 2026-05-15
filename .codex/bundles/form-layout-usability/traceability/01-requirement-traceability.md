# Requirement Traceability

| Raw Note | Normalized Requirements | Owning Subbundle | Planned Proof |
| --- | --- | --- | --- |
| Forms are not properly stretched to use available space. | FRM-001, FRM-002, FRM-008 | 01 shared foundation, 02 module layouts | Source inventory, shared component diff, before/after form screenshots. |
| Textareas expecting larger text need proper default sizes. | FRM-003 | 01 shared foundation, 02 module layouts | TextArea default diff, `.cda-input--textarea` CSS diff, screenshots of notes/prompt/instructions fields. |
| Too many fields sometimes should be split into subtabs by topic. | FRM-004 | 02 module layouts | Process editor screenshots showing topical tabs or equivalent grouping. |
| Analyze all forms across the app and screenshots. | FRM-001, FRM-006, FRM-007 | 03 validation checklist and proof | Inventory and `.xlsx` checklist with screenshot/proposal columns. |
| For each screenshot create an imagegen proposal of only the form area. | FRM-006 | 03 validation checklist and proof | Proposal image path per screenshot row. |
| Improve layout based on proposals; add icons or aesthetics for enterprise clarity. | FRM-005, FRM-007, FRM-008 | 01 shared foundation, 02 module layouts | Code diff and comparison row per implemented change. |
| Use a proper xlsx checklist with file references. | FRM-001, FRM-007 | 03 validation checklist and proof | Final workbook link and rendered verification. |

## Actual Proof

| Raw Note | Closure | Evidence |
| --- | --- | --- |
| Forms are not properly stretched to use available space. | Closed | `FormField`, `FormSection`, `Grid`, and surface CSS diffs; post-change screenshots in `C:\repositories\CanDoItAll\output\form-layout-usability\screenshots`. |
| Textareas expecting larger text need proper default sizes. | Closed | `TextArea.Rows`, `.cda-input--textarea`, and module textarea class updates; workbook rows FORM-001, FORM-005, FORM-008, FORM-009. |
| Too many fields sometimes should be split into subtabs by topic. | Closed | `ProcessDefinitionForm.razor` and `CandidatePipeline.razor` use topical tabs; screenshots FORM-002 and FORM-003. |
| Analyze all forms across the app and screenshots. | Closed | `inventories/01-scope-inventory.md` and `C:\repositories\CanDoItAll\output\form-layout-usability\form-layout-checklist.xlsx`. |
| For each screenshot create an imagegen proposal of only the form area. | Closed | Six proposal PNGs in `C:\repositories\CanDoItAll\output\form-layout-usability\proposals`. |
| Improve layout based on proposals; add icons or aesthetics for enterprise clarity. | Closed | Shared `FormSection.Icon` support plus targeted iconized actions/sections in process, CRM/HR, settings, and project structure forms. |
| Use a proper xlsx checklist with file references. | Closed | Workbook exported at `C:\repositories\CanDoItAll\output\form-layout-usability\form-layout-checklist.xlsx`; render/inspect pass completed. |
