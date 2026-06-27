# SB02 Semantic Invariants

## Invariant SB02-USAGE-CONTEXT

- Invariant ID: `SB02-USAGE-CONTEXT`
- Source raw note: N004, N005
- Expected behavior: Manager chat must have selected-run cost and token context when telemetry is loaded, and ordinary cost/token prompts must keep runtime tools available.
- Disallowed shallow implementation: Merely telling the manager to use tools is insufficient if the Manager tab never loads usage telemetry or if the classifier disables tools for natural cost/token wording.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-usage-context.txt`
- Passing test: `bundle://proof/SB02/transcripts/passing-usage-context-component-test.txt`; `bundle://proof/SB02/transcripts/passing-classifier-test.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` before `9e3402ad9835b655c8120aa46ee0f2a502e1bf17d81807fd7cb85eecc8b56f57` after `630d417e9243ac19e701a9b298eee66a42fd7ae41918cae006ff1e1e1f683d5a`; `repo://src/CanDoItAll.Modules.Processes/Services/ProcessManagerChatPromptClassifier.cs` before `10ab7a17b97cc47e582d780119a7e07e5a3e5298f54f6e1a9fb6c66ad33d5f05` after `ed55488e01ad04ede7b1c10f3f5ee315c47b834fc377fae52135da1f14fe0957`; component and unit test files after `f6406621d78fdb7e143e7d5bdf27b0813f4aafce755229da3b11ee34ae04b5cd` and `addb673f50c3f6f57afca6ad6d664142221de198cf049b23ac778355bbe4a91c`.
- Production assertions: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` includes `IncludeUsageTelemetry = true`, selected-run auto-selection for Manager chat, and `BuildManagerChatSelectedRunUsageText`; `repo://src/CanDoItAll.Modules.Processes/Services/ProcessManagerChatPromptClassifier.cs` limits tool disabling to explicit preloaded-context-only wording.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first-usage-context.txt` rejects the old no-usage-telemetry condition by failing when Manager load options request usage telemetry.
- Downstream dependency check: `bundle://proof/SB03/transcripts/passing-5032-health.txt` proves the rebuilt host can serve the Manager surface that consumes this prompt context.
