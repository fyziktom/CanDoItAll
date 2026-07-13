# Requirement Traceability

| Raw Note | Exact Wording | Requirements | Owner | Proof Method | Status |
| --- | --- | --- | --- | --- | --- |
| `N001` | "move that into some dialog" | `REQ-001`, `REQ-002`, `REQ-004` | `SB02` | `bundle://proof/SB02/manifest.md`, `bundle://proof/SB04/browser/workflow-template-catalogue-dialog-large-offer-filter.png` | `Solved` |
| `N002` | "it must be as button on Workflows tab" | `REQ-002` | `SB02` | `bundle://proof/SB02/manifest.md`, `bundle://proof/SB04/transcripts/sb04-browser-validation.txt` | `Solved` |
| `N003` | "catalogue of workflows templates then must be as dialog loaded only when someone will open that dialog. otherwise it does not load the templates." | `REQ-003`, `REQ-004` | `SB02` | `bundle://proof/SB02/manifest.md`, `bundle://proof/SB04/transcripts/sb04-component-tests.txt` | `Solved` |
| `N004` | "In the dialog it must be possible to see basic description and button to show preview." | `REQ-004` | `SB02` | `bundle://proof/SB04/browser/workflow-template-catalogue-dialog-large-offer-filter.png` | `Solved` |
| `N005` | "it will open dialog with workflow canvas so they can see how workflows looks like" | `REQ-005` | `SB03` | `bundle://proof/SB03/manifest.md`, `bundle://proof/SB04/browser/workflow-template-preview-dialog-large.png` | `Solved` |
| `N006` | "if they like it they can 'add to my drafts'" | `REQ-006` | `SB03` | `bundle://proof/SB03/manifest.md`, `bundle://proof/SB04/transcripts/sb04-component-tests.txt` | `Solved` |
| `N007` | "if same workflow is already there, it must name it with some prefix like 01, 02, etc." | `REQ-007` | `SB03` | `bundle://proof/SB03/manifest.md`, `bundle://proof/SB04/transcripts/sb04-component-tests.txt` | `Solved` |
| `N008` | "Use imagegen ... to create UI design proposals for all dialogs separatelly" | `REQ-008` | `SB01` | `bundle://evidence/design/template-catalogue-dialog-proposal.png`, `bundle://evidence/design/template-preview-dialog-proposal.png` | `Solved` |
| `N009` | "use those designs to build the dialogs layouts with use of our components to be close enough (you must validate that with screenshots against proposals)" | `REQ-008`, `REQ-010` | `SB04` | `bundle://proof/SB04/visual-comparison-notes.md` | `Solved` |
| `N010` | "rename workflows that contains SEAMARK" | `REQ-009` | `SB04` | `bundle://proof/SB04/transcripts/sb04-debranding-source-search.txt`, `bundle://proof/SB04/transcripts/sb04-template-unit-tests.txt` | `Solved` |
| `N011` | "workflow should be able to do generic analysis of the offer to get summary info" | `REQ-009` | `SB04` | `bundle://proof/SB04/transcripts/sb04-template-unit-tests.txt` | `Solved` |
| `N012` | "templates of workflows should be as kind of generic examples for new users. we should avoid exact names and sensitive informations." | `REQ-009` | `SB04` | `bundle://proof/SB04/transcripts/sb04-debranding-source-search.txt` | `Solved` |
| `N013` | "large screen only ... skip small and medium screens tests" | `REQ-010` | `SB04` | `bundle://proof/SB04/transcripts/sb04-browser-validation.txt`, `bundle://reviews/01-execution-report.md` | `Solved` |
