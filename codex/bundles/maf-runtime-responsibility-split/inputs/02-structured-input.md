# Structured Input

## Objectives

- Reduce `MafAgentRuntime.cs` size and responsibility density by extracting cohesive collaborators.
- Replace partial-class responsibility grouping with real classes where the extracted behavior has a clear owner.
- Move `ComputeStableHash` or equivalent stable text/content hashing into a reusable whole-project helper when dependency direction allows it.
- Keep `FormatArgumentValue` or equivalent argument-summary formatting as a MAF-specific helper unless current-state review finds broader, compatible reuse.
- Turn session, model parameters, and context manifest logic into builder/collaborator classes rather than partial members of `MafAgentRuntime`.
- Isolate finalizer behavior as a driver, strategy, or helper boundary with explicit tests and failure behavior.
- Include detailed implementation, test, and UI validation checklists in an `.xlsx` workbook.
- Repair agent-chat execution for local providers so agents configured for Local Ollama actually send requests to Ollama using the provider-compatible local model instead of retaining managed-seed OpenAI defaults.
- Repair local Playwright MCP setup/runtime so agent chat can discover and invoke browser tools through the same real runtime path used by the app, not a mocked tool path.

## Hard Constraints

- Prepare bundle only; do not implement production changes in this phase.
- Preserve existing MAF runtime behavior, including finalizer sequencing, recovery, required-finalizer repair, provider usage, approvals, tool traces, session compatibility, request-scoped attachment removal, and context manifests.
- Keep C# strongly typed. Avoid new magic-string identifiers for helper choices, driver modes, or finalizer commands.
- Do not add hidden fallback mechanisms. Any retry, repair, or recovery path must remain explicit and logged/tested.
- Use existing test projects and Playwright fixtures before adding new harnesses.
- UI validation must include real browser proof, not only component or API tests.
- Follow-up repair validation must use the real web app and real Local Ollama provider state. It must not replace the provider or MCP path with test stubs for acceptance proof.

## Assumptions

- `src/Foundation/CanDoItAll.SharedKernel` is the preferred candidate for cross-solution stable hashing, subject to implementation-time dependency review.
- MAF-specific formatting helpers belong inside the MAF project unless a concrete non-MAF caller is found.
- Extracted builders can start as internal concrete classes. Interfaces should be added only when tests or a real boundary need them.
- The existing `MafAgentRuntime` public contract should remain stable.
- Seeded agents may carry managed OpenAI model names even after their provider profile is switched to Local Ollama; runtime model resolution must account for that mismatch without changing custom explicitly supported local models.

## Risks

- Moving finalizer code can subtly change validation order, tool-trace sequencing, or session serialization.
- Moving session code can break provider-managed conversation restoration and approval continuation behavior.
- Moving hashing helpers can create dependency direction problems if placed too low or too high in the solution.
- UI behavior can regress even when the refactor is backend-only because chat/workflow/process screens depend on runtime responses and diagnostics.
- Provider health and workflow LLM calls can succeed while agent chat fails if the chat runtime resolves the model differently from provider setup/workflow execution.
- Playwright MCP setup can pass discovery but still fail during agent chat if setup-time launch/framing differs from runtime launch/framing.

## Validation Expectations

- Use focused unit tests for each extracted collaborator.
- Use integration tests for finalizer, run tracking, recovery, session compatibility, and context manifest behavior.
- Use static scans to prove `MafAgentRuntime.cs` shrinks and no extracted responsibility is copied into a new large catch-all file.
- Use Playwright browser validation for `/agents`, `/agents?tab=agents`, `/agents?tab=capabilities`, `/agents/workflows`, and process/workflow smoke surfaces after runtime refactor closure.
- Record command transcripts, changed-file hashes, source assertions, anti-stub audit, and browser artifacts under `proof/SBxx/` during execution.
- For SB09, record both API and UI proof that Local Ollama agent chat completes, and proof that a UI-started agent chat invokes `browser_navigate` and `browser_snapshot` through local Playwright MCP with persisted tool receipts.
