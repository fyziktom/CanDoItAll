# Requirement Traceability

## Input Coverage Matrix

| Raw input | Exact wording | Normalized requirements | Owning subbundles | Planned proof |
| --- | --- | --- | --- | --- |
| N001 | `MafAgentRuntime.cs is too large.` | R02, R10 | SB01, SB07, SB08 | Line-count inventory, static scan, build/test proof, changed-file hashes. |
| N002 | `split it based on responsibilities and isolate helpers` | R02, R09, R10 | SB01-SB07 | Responsibility map, extracted collaborator tests, source assertions. |
| N003 | `finalizers could be as drivers or some strategies... at least as helpers isolation` | R03, R10 | SB06, SB08 | Finalizer semantic gate, negative/positive integration tests, anti-stub audit. |
| N004 | `ComputeStableHash... should be in some general helpers of whole project` | R04 | SB02 | Shared helper dependency scan, exact hash output tests, reuse source assertions. |
| N005 | `FormatArgumentValue... more some MAF helper and not whole sln helper` | R05 | SB02 | MAF formatter tests and scan proving it left `MafAgentRuntime`. |
| N006 | `partial classes... still means that it is mixing lots of responsibilities` | R02, R09 | SB01, SB07 | Static scan for residual partial responsibility and new catch-all files. |
| N007 | `MafAgentRuntime.ModelParameters should be as kind of builder` | R07 | SB04 | Builder tests for temperature, retry, reasoning effort, model resolution. |
| N008 | `Same session and context manifest. It should be SessionBuilder and ContextManifestBuilder.` | R06, R08 | SB03, SB05 | Builder tests and integration proof for session/context behavior. |
| N009 | `first prepare bundle only` | R01 | SB01 | No production source changes in preparation; bundle validator passes. |
| N010 | `use xlsx to create detailed checklists... including UI testing` | R11, R12 | SB01, SB08 | `bundle-checklists.xlsx`, workbook visual verification, Playwright proof plan. |

## Follow-Up Input Coverage Matrix

| Raw input | Exact wording | Normalized requirements | Owning subbundles | Planned/proven proof |
| --- | --- | --- | --- | --- |
| N011 | `analyze that trouble with provider and repair it` | R13-R17 | SB09 | Root-cause analysis, source changes, focused tests, live app proof. |
| N012 | `gptoss20b64k... project structure... does not responded... GPU... not even started loading model in vram` | R13, R16 | SB09 | API and UI project-structure/agent-chat Local Ollama proof with provider/model in run detail. |
| N013 | `health check in provider setup... worked ok and ollama loaded model` | R13 | SB09 | Compare provider health/setup success with repaired chat runtime model resolution. |
| N014 | `Financial Manager has setting to local ollama provider` | R13, R14 | SB09 | Runtime model resolver uses configured provider default for managed-seed OpenAI model names. |
| N015 | `gemma4-12b-256k... same result` | R13, R14 | SB09 | Unit and live proof cover `gemma4-12b-256k` as the Local Ollama model used by agent chat. |
| N016 | `same thing in agents chat... it does not send anything to ollama` | R13, R16 | SB09 | Agents-page UI proof completes through Local Ollama and persisted run details show model/provider. |
| N017 | `workflow with simple llm call... worked... trouble is just in agents` | R13, R16 | SB09 | Repair is scoped to agent-chat model resolution and runtime MCP attachment, not workflow execution. |
| N018 | `try also with playwright mcp to chat with agents via UI... do not fake those tests` | R15, R16, R17 | SB09 | Live setup-test proof, UI chat screenshot/DOM proof, persisted tool receipts for `browser_navigate` and `browser_snapshot`. |

## Requirement To Subbundle Matrix

| Requirement | SB01 | SB02 | SB03 | SB04 | SB05 | SB06 | SB07 | SB08 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R01 | Owner |  |  |  |  |  |  | Reviewer |
| R02 | Owner | Support | Support | Support | Support | Support | Owner | Reviewer |
| R03 | Support |  | Support |  |  | Owner | Support | Reviewer |
| R04 |  | Owner |  |  |  |  | Reviewer | Reviewer |
| R05 |  | Owner |  |  |  |  | Reviewer | Reviewer |
| R06 | Support | Support | Owner |  |  | Support | Reviewer | Reviewer |
| R07 | Support | Support |  | Owner |  |  | Reviewer | Reviewer |
| R08 | Support | Support |  |  | Owner |  | Reviewer | Reviewer |
| R09 | Owner | Owner | Owner | Owner | Owner | Owner | Owner | Reviewer |
| R10 | Support | Support | Owner | Owner | Owner | Owner | Owner | Owner |
| R11 | Owner |  |  |  |  |  |  | Reviewer |
| R12 |  |  |  |  |  |  | Support | Owner |

## Follow-Up Requirement To Subbundle Matrix

| Requirement | SB09 |
| --- | --- |
| R13 | Owner |
| R14 | Owner |
| R15 | Owner |
| R16 | Owner |
| R17 | Owner |

## Proof Destinations

| Subbundle | Future proof path | Required contents |
| --- | --- | --- |
| SB01 | `bundle://proof/SB01/manifest.md` | Inventory source assertions, line-count transcript, threshold decision, anti-stub scan if characterization tests are added. |
| SB02 | `bundle://proof/SB02/manifest.md` | Hash/formatter tests, dependency direction scan, source assertions, changed-file hashes. |
| SB03 | `bundle://proof/SB03/manifest.md` | Session builder tests, approval/session integration tests, source assertions, negative proof for attachment/provider history regression. |
| SB04 | `bundle://proof/SB04/manifest.md` | Model option tests, unsupported transport diagnostics tests, source assertions. |
| SB05 | `bundle://proof/SB05/manifest.md` | Context manifest tests, source assertions, token estimate proof. |
| SB06 | `bundle://proof/SB06/manifest.md` | Finalizer semantic invariants, negative and positive integration tests, recovery proof, usage proof, anti-stub audit. |
| SB07 | `bundle://proof/SB07/manifest.md` | Static size scans, no catch-all helper scan, MAF build, source assertions. |
| SB08 | `bundle://proof/SB08/manifest.md` | Full regression commands, Playwright transcripts, screenshots, analytics review, raw-note closure. |
| SB09 | `bundle://proof/SB09/manifest.md` | Local provider root-cause proof, focused unit/integration/web build transcripts, live API run details, live UI screenshots/DOM snapshots, Playwright MCP setup and runtime tool receipts, cleanup proof, anti-stub audit. |
