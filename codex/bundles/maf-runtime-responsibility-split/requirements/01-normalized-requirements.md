# Normalized Requirements

| ID | Requirement | Acceptance signal | Owner |
| --- | --- | --- | --- |
| R01 | Preserve the raw request and prepare an implementation-ready bundle only. | Bundle contains raw input, structured requirements, subbundle plan, traceability, reviews, and no production source changes. | SB01 |
| R02 | Split `MafAgentRuntime.cs` by responsibility rather than relying on partial classes as the final design. | Runtime delegates to extracted collaborators and no new catch-all partial is introduced. | SB07 |
| R03 | Isolate finalizer behavior as a driver, strategy, or focused helper boundary. | Required-finalizer repair, JSON repair, streamed capture, recovery, sequence validation, and response building are owned by named finalizer collaborators with tests. | SB06 |
| R04 | Move `ComputeStableHash` or equivalent stable hashing into a general project helper when dependency direction permits. | Shared helper exists in an approved foundation location; existing MAF output is preserved or intentionally renamed/tested; process hashers are not duplicated blindly. | SB02 |
| R05 | Keep `FormatArgumentValue` or equivalent argument-summary formatting as a MAF helper unless broader reuse is proven. | Argument formatting is moved out of `MafAgentRuntime` into a focused MAF helper with unit tests for primitives, JSON, collections, truncation, and hash suffixes. | SB02 |
| R06 | Extract session creation, restoration, prompt input, streaming snapshot, run options, structured response format, and history-mode decisions into a `SessionBuilder` or equivalent collaborator. | `MafAgentRuntime.Session.cs` is replaced by a focused builder class; approval continuation and provider-managed history tests pass. | SB03 |
| R07 | Extract model parameter construction into a `ModelParametersBuilder` or equivalent collaborator. | Temperature omission/retry, reasoning effort, model resolution, and diagnostics are covered by direct tests and runtime tests. | SB04 |
| R08 | Extract context manifest creation into a `ContextManifestBuilder` or equivalent collaborator. | Manifest totals, source records, tool schema estimates, and exclusion/inclusion behavior are direct-tested and integration-tested. | SB05 |
| R09 | Keep strongly typed boundaries and avoid magic strings for new identifiers, modes, or commands. | New collaborators use records/options/enums/constants and do not add raw string switches for internal decisions. | SB01-SB07 |
| R10 | Preserve existing finalizer, session, tool, provider, and context behavior. | Focused unit tests, integration tests, and semantic proof manifests pass before downstream work continues. | SB03-SB08 |
| R11 | Include detailed `.xlsx` checklists covering implementation, tests, risks, traceability, and UI validation. | `bundle-checklists.xlsx` exists, renders legibly, and contains the planned checklist sheets. | SB01 |
| R12 | Include UI testing after the runtime refactor. | Playwright proof covers `/agents`, `/agents?tab=agents`, `/agents?tab=capabilities`, `/agents/workflows`, and process/workflow smoke routes with screenshots and assertions. | SB08 |
| R13 | Repair local-provider agent chat so Local Ollama agents actually send configured provider-compatible local models. | API and UI chat proof show Local Ollama runs completing with model `gemma4-12b-256k`, including project-structure and agents-page chat surfaces. | SB09 |
| R14 | Preserve custom/supported local model choices while only falling back from managed-seed OpenAI defaults to the local provider default. | Unit tests prove known managed seed OpenAI models use the Ollama provider default, while supported local/custom models are preserved. | SB09 |
| R15 | Repair local Playwright MCP setup and runtime launch/framing for agent chat. | Live setup-test proof discovers Playwright tools with schemas, runtime logs attach 29 tools, and persisted receipts include local MCP launch plus `browser_navigate`/`browser_snapshot`. | SB09 |
| R16 | Prove the repair through real app UI and API flows, not mocked providers or fake MCP tools. | Proof artifacts include live API run details, browser screenshots/DOM snapshots, persisted execution run details, and cleanup verification for disposable agents. | SB09 |
| R17 | Update bundle/checklist evidence for the follow-up provider/MCP regression. | SB09 README, proof manifest, semantic invariants, traceability rows, execution report, and regenerated workbook include the provider/MCP repair and tests. | SB09 |

## Scope Exceptions

- This bundle does not require refactoring `MafAgentRuntime.AgentFactory.cs` unless SB01 or SB07 proves it must change to keep responsibilities coherent.
- This bundle does not require moving existing capability partials. They are adjacent risk surfaces, not the primary target.
- This bundle does not require introducing interfaces for each new collaborator. Concrete internal classes are preferred unless tests or dependency boundaries require abstraction.
- SB09 does not replace the provider setup UI or workflow LLM execution paths. It fixes the agent-chat model-resolution and local MCP runtime path proven by the reported local-provider symptoms.
