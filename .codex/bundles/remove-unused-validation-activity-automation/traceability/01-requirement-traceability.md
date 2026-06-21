# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| R001 Remove all three old modules | `requirements/01-normalized-requirements.md` | `subbundles/03-03-project-module-and-test-removal` | `bundle://proof/SB03/transcripts/direct-reference-audit.txt`; build transcript | Direct runtime/project references must be gone. |
| R002 Remove related tests | `requirements/01-normalized-requirements.md` | `subbundles/03-03-project-module-and-test-removal` | `bundle://proof/SB04/transcripts/tests.txt` | Delete module-specific tests; update incidental references. |
| R003 Reference map first | `inventories/01-scope-inventory.md` | `subbundles/01-01-reference-inventory-and-removal-boundaries` | `bundle://inventories/unused-module-reference-map.xlsx` | XLSX prepared before product edits. |
| R004 Project-structure connections | `architecture/01-target-solution.md` | `subbundles/03-03-project-module-and-test-removal` | Workbench diff and direct-reference audit | Covers right-click menu and quick actions. |
| R005 Scheduler replaces automation path | `architecture/01-target-solution.md` | `subbundles/02-02-module-dependency-extraction` | SchedulerPlanner source audit and tests/build | Must precede Automation project deletion. |
| R006 Remove UI routes/cards | `requirements/01-normalized-requirements.md` | `subbundles/03-03-project-module-and-test-removal` | Browser nav check in SB04 | Routes should not be advertised. |
| R007 App works | `reviews/01-execution-report.md` | `subbundles/04-04-build-browser-and-bundle-closure` | Build, tests, Browser proof | Closure gate. |
| R008 Port 5032 rebuild | `reviews/01-execution-report.md` | `subbundles/04-04-build-browser-and-bundle-closure` | `bundle://proof/SB04/transcripts/port-5032-restart.txt` | Existing process already stopped. |
| R009 Scope control | `analysis/02-assumptions-and-risks.md` | `subbundles/01-01-reference-inventory-and-removal-boundaries` | Workbook keep/remove categories | Generic unrelated terms stay. |
