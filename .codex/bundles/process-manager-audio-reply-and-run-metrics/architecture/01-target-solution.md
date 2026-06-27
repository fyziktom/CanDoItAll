# Target Solution

- Extend the existing Manager chat send path with the same post-response speech behavior used by other chat surfaces: after `SendMessageAsync` returns, if `managerChatVoiceModeEnabled` is true and the assistant response has content, call `SpeakManagerChatTextAsync`.
- Change Manager chat runtime load options to include selected-run usage telemetry while keeping history, metric history, and active agents disabled. This gives the manager cost/token totals without expanding the tab into a full analytics query.
- Add a selected-run usage section to `BuildManagerChatPrompt` using strongly typed projection fields from `CurrentShell.Runtime.Stats`. Prefer actual cost when available; otherwise show estimated cost and label it.
- Tighten `ProcessManagerChatPromptClassifier` so natural cost/token questions do not disable runtime tools. Only disable runtime/workspace tools when the user explicitly asks to use only the preloaded/already-loaded context.
- Validate through focused component/unit tests, then build, restart `http://localhost:5032`, and run browser proof.
