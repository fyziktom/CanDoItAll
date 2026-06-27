# SB02 Proof Manifest

- Subbundle id: `SB02-manager-selected-run-usage-context`
- Status: `Completed`
- Owned requirements: R003, R004, R005
- Raw notes: N004, N005
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` | `9e3402ad9835b655c8120aa46ee0f2a502e1bf17d81807fd7cb85eecc8b56f57` | `630d417e9243ac19e701a9b298eee66a42fd7ae41918cae006ff1e1e1f683d5a` |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessManagerChatPromptClassifier.cs` | `10ab7a17b97cc47e582d780119a7e07e5a3e5298f54f6e1a9fb6c66ad33d5f05` | `ed55488e01ad04ede7b1c10f3f5ee315c47b834fc377fae52135da1f14fe0957` |
| `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` | `4dec0917f0fdbd1180ce141b4e0de948b41b1b8f2df27cc23e4f7a2db6f355de` | `f6406621d78fdb7e143e7d5bdf27b0813f4aafce755229da3b11ee34ae04b5cd` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerChatPromptClassifierTests.cs` | `e29d14bf9c44faeb37ff2e9a4717ec3055980b322237303fe2abfc873f75d089` | `addb673f50c3f6f57afca6ad6d664142221de198cf049b23ac778355bbe4a91c` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-usage-context.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing-usage-context-component-test.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing-classifier-test.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` requests selected-run usage telemetry for the Manager tab and adds selected-run usage metrics to the prompt.
- Source proof: `repo://src/CanDoItAll.Modules.Processes/Services/ProcessManagerChatPromptClassifier.cs` keeps runtime tools enabled for natural cost and token questions unless the prompt explicitly requests preloaded context only.
- Test name: `Manager_chat_prompt_includes_selected_run_usage_and_keeps_runtime_tools_enabled_for_cost_token_question`
- Test name: `ShouldDisableRuntimeTools_returns_false_for_natural_cost_token_question`
- Test proof: `bundle://proof/SB02/transcripts/passing-usage-context-component-test.txt`
- Test proof: `bundle://proof/SB02/transcripts/passing-classifier-test.txt`
- Adversarial negative proof: `bundle://proof/SB02/transcripts/failing-first-usage-context.txt`
- Anti-stub audit: No production stubs found in `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.

## Browser Or Host Proof

- Downstream host proof: `bundle://proof/SB03/transcripts/passing-5032-health.txt`
