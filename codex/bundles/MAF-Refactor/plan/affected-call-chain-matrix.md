# Affected call-chain matrix

| Chain | Entry -> terminal behavior | Subbundles | Required parity |
|---|---|---|---|
| Floating send | AgentChatPanel -> Orchestrator -> context capture -> authority -> run start -> execution port -> persistence -> completion notification | SB02, SB03, SB09, SB17 | transcript, activity, context source, usage, refresh |
| Approval resume | UI/API -> run lookup -> decision persistence -> original turn/authority -> continuation port -> runtime state -> persistence | SB09, SB15, SB17 | exact proposal IDs, no current-context recapture |
| Provider diagnostics | UI/service -> diagnostics port -> provider runtime -> driver | SB09, SB12 | health/models/errors without agent graph |
| Agent execution | Core coordinator -> scope services -> capability composition -> MAF adapter -> provider/tool loop -> finalizer -> persistence | SB06-SB11 | all traces/usage/session/cleanup |
| Process step | Processes dispatcher -> Core execution -> MAF -> typed result/failure -> Processes recovery/completion gates | SB12-SB14, SB17 | gates once, current evidence, branch/receipts |
| Workflow LLM | workflow node -> lightweight port -> provider runtime/driver -> workflow schema/usage | SB16, SB17 | text/JSON/usage/failure/cancellation |
| Hosted/A2A | hosting endpoint/tool -> hosted-agent port/factory -> MAF/A2A | SB09-SB12 | identity, participants, disposal, credentials |
| Project/Gantt context | page/Gantt projection -> observation publication -> next-turn capture | SB03-SB05 | bounded view facts, no canonical duplication |
| Public API | API endpoint -> application service -> public projection | SB09, SB15, SB17-SB18 | no runtime/authority/private payload leakage |
| Future ordinary chat | conversation service -> lightweight port -> transcript persistence | SB16 design/future | no agent concepts, ordered transcript, usage |
