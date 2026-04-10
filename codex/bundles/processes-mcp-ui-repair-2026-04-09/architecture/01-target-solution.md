# Target Solution

- `ProcessWorkspace` performs one initial load even when its route parameters are all null, then keeps the existing change-detection short circuit for later parameter repeats.
- `ProcessesService.ListDefinitionsAsync` resolves a single summary version per definition and derives role/step counts from that version only.
- Verification is split across a component render for the first-load defect, an integration assertion for the summary-count defect, and an end-to-end browser check against the running web app.
- No transport, token, or configuration changes are part of this repair.
