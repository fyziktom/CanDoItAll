# Input Coverage Matrix

| Raw note | Exact source wording | Normalized requirements | Owning subbundle | Planned proof | Status |
| --- | --- | --- | --- | --- | --- |
| `N001` | "we must be able to run workflow from project structure canvas." | `R001`, `R003`, `R006`, `R011` | `01`, `03`, `04`, `07` | Backend API tests and Playwright start proof | `Planned` |
| `N002` | "we have similar system for starting processes." | `R002`, `R011` | `01`, `03` | Code review against process start service/API pattern | `Planned` |
| `N003` | "First user will add process node in project structure and then they start with via right click opiton." | `R002`, `R004`, `R006` | `02`, `04` | UI/component tests for add-then-start flow | `Planned` |
| `N004` | "First user will add workflow node under some node." | `R002`, `R005` | `01`, `02` | Project-structure create tests verify parent id | `Planned` |
| `N005` | "During adding it opens dialog where is possible to select specific workflow." | `R003`, `R004` | `02`, `04` | Add dialog component and browser proof | `Planned` |
| `N006` | "settings must be little more advanced because we must specify what we want to provide as input for the workflow." | `R004`, `R005` | `02` | Input contract unit tests and UI preview assertions | `Planned` |
| `N007` | "prefilled info about parent node with all details." | `R005` | `02`, `03` | Input payload snapshot test includes full parent node details | `Planned` |
| `N008` | "always provide also information about what project is it." | `R005` | `02`, `03` | Input payload snapshot test includes project id/title/status | `Planned` |
| `N009` | "click in right click menu to start. It must open confirmation dialog to confirm start." | `R006` | `04` | Context-menu Playwright proof and component tests | `Planned` |
| `N010` | "does not need matching resources dialog during start as we have in processes." | `R006` | `03`, `04` | UI test confirms no staffing/resource stage | `Planned` |
| `N011` | "inform user about that it is running." | `R007`, `R008` | `03`, `04` | Status/progress test and browser screenshot | `Planned` |
| `N012` | "setup progress of workflow node ... to started and when it is done to 100%." | `R007` | `03` | Backend status mapping tests | `Planned` |
| `N013` | "if it fails, pause,etc we can add proper marker too." | `R007` | `03` | Failure/cancelled state tests verify markers | `Planned` |
| `N014` | "click in project structure on workflow node it can show in selection floating window actual status in little more detail." | `R008` | `04` | Selection panel component test and Playwright screenshot | `Planned` |
| `N015` | "At least what step from how much is it now and some generic info about status" | `R008` | `03`, `04` | Event/progress projection tests | `Planned` |
| `N016` | "workflow is adding some nodes with results it must add them under yourself node" | `R009` | `05` | Project-structure executor/projection tests | `Planned` |
| `N017` | "Each workflow should provide execution summary in the project structure too." | `R010` | `05` | Summary node/metadata tests and browser proof | `Planned` |
| `N018` | "created some new files it must contains list of them" | `R010` | `05`, `06` | File-writing scenarios verify path list in summary | `Planned` |
| `N019` | "provide also path in this summary" | `R010` | `05`, `06` | Summary validation checks storage paths | `Planned` |
| `N020` | "First assure ... backend. Then go to the UI layer." | `R011` | `01`, `03`, `04` | Phase gates block UI until backend tests pass | `Planned` |
| `N021` | "test it on real cases (use gpt-5-mini and gptoss20b64k local ollama)." | `R012`, `R014` | `06`, `07` | Provider-specific scenario proof | `Planned` |
| `N022` | "Use at least 20 different real world cases." | `R012` | `06`, `07` | Scenario matrix with at least 20 completed rows | `Planned` |
| `N023` | "synthesize data ... emails, or some simple business plan as md" | `R013` | `06` | Synthetic data artifact inventory | `Planned` |
| `N024` | "files here C:\programovani\testdata\testworkflows" | `R013`, `R014` | `06`, `07` | Scenario harness reads listed files/folders | `Planned` |
| `N025` | "same db postgresql as we have now in visual studio instance" | `R014` | `07` | PostgreSQL-backed validation log | `Planned` |
| `N026` | "If ... troubles ... add subbundle to repair it on the fly." | `R015` | `07` | Execution report records reopen/subbundle decisions | `Planned` |
