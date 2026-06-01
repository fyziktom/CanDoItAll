# Normalized Requirements

| ID | Requirement | Validation |
| --- | --- | --- |
| R01 | The default multi-team delivery template must be explicitly suitable for .NET delivery and keep JavaScript separation out of scope. | Source assertion on `software-delivery` summary, tailoring rules, and tests. |
| R02 | The process must classify the requested .NET app type, including backend-only, Blazor SSR, Blazor WASM, and Blazor WASM PWA. | Required artifact expectation and test assertions for app-type terms. |
| R03 | Architecture design and review must be split into a dedicated subprocess with separate design and review steps. | New `dotnet-architecture-design-review` template plus subprocess reference from `software-delivery`. |
| R04 | Architecture and review steps must not have product mutation permissions. | Operation contract tests: no `MutateProductTarget`, read-only target scope. |
| R05 | .NET implementation must be routed through a .NET subprocess rather than a direct parent process coding step. | `software-delivery` implementation step is `Subprocess` targeting `.NET implementation slice with atomic validation`. |
| R06 | QA/review/writeback/screenshot/runtime-command steps must be non-mutating. | Operation contract tests on affected templates. |
| R07 | UI-capable apps must capture screenshots through a subprocess and store accepted screenshots under `Screenshots` below the process run node. | New screenshot writeback subprocess text and tests. |
| R08 | Every .NET run must write project-structure runtime command nodes under `Run command` below the process run node, with at least `Run app` and `Run tests`. | New runtime command writeback subprocess text and tests. |
| R09 | The implementation must not run the delivery process. | Execution report records no process-run command/API was invoked. |
| R10 | The app should be left running after implementation for user-led process tests. | Final handoff lists the URL/process details or blocker. |
