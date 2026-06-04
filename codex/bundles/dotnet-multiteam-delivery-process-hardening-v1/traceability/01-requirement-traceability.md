# Requirement Traceability

| Raw Note | Normalized Requirements | Owning Subbundle | Proof Method |
| --- | --- | --- | --- |
| Improve multi-team software delivery process. | R01, R05 | SB02 | Source assertions and process-template tests. |
| Harden permissions so architect will not start coding. | R04, R06 | SB02 | Operation contract tests on architecture/review steps. |
| Fit to multi-team .NET software delivery, JS later. | R01 | SB02 | Template summary/tailoring source assertion. |
| Recognize backend, Blazor SSR, Blazor WASM, etc. | R02 | SB02 | Required app-type artifact and tests. |
| Use proper subprocesses. | R03, R05, R07, R08 | SB02, SB03 | Subprocess template definitions and import tests. |
| UI apps must take screenshots into project structure under `Screenshots` under process run node. | R07 | SB03 | Source assertion and governance test. |
| Add runtime dotnet nodes under `Run command` under process run node, at least run app and run tests. | R08 | SB03 | Source assertion and governance test. |
| Add subprocess for architecture design and review. | R03 | SB02 | New template and tests. |
| Review architecture for component logic split, models, services, user stories, testability. | R03, R04 | SB02 | Architecture review step artifact contract and tests. |
| Do not run the process. | R09 | SB04 | Execution report command log. |
| Keep app running for user tests. | R10 | SB04 | Final handoff. |
