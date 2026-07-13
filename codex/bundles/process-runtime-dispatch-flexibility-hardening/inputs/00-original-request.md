# Original Request

Use `candoitall-bundle-workflow` to solve this.

Main goal:

improve last changes to assure better flexibility of runtime/dispatcher of processes

Architect notes:

- You did lots of changes in last 3 days run. It is in last commit, first commit in this branch. I did fast review and lots of them looks logical and that they improve our processes in general way. But we have some parts of the code that are not well done. They might work, but they are mixing responsibilities and they are hard to maintain. Typical adept for refactoring is `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`. It is crazy long file that mixing too many things together. It must be split to logical helpers, shared abstractions, or reusable drivers. All those builders of parts of prompts must be isolated to proper drivers or strategies so we can easily mock them or change them based on the model of AI.
- The example above is just one of the similar situations across the process runtime parts.
- We are testing it mostly on building some application, usually multiteam development process, but processes runtime and dispatch must be able to do any other type of tasks in enterprise companies. It includes business analysis, supplier analysis, reports preparation, quality management reports and testing, etc. Main process parts must be flexible enough and domain-specific parts must be isolated in own drivers and projects.

Requested workflow:

- Do detailed analysis and architecture improvements.
- First prepare just bundle and do not do implementation.
- Do not skip anything.
- Do not simplify anything.
- Do not remove functionality because it is disliked.
- Preserve all functionality and make it more maintainable with better isolation of domain-specific parts.
