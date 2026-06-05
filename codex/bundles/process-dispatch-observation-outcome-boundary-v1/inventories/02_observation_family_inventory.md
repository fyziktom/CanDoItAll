# Observation Family Inventory

| Observation family | Existing source | Target helper | Notes |
| --- | --- | --- | --- |
| successful session tool names | ToolValidation.cs | ProcessAutomationSessionObservation | preserve call/result success filtering |
| session tool result texts | ToolValidation.cs | ProcessAutomationSessionObservation | preserve depth limit and diagnostic properties |
| session file writes | ToolValidation.cs | ProcessAutomationSessionObservation | preserve path/content extraction |
| session file reads | ToolValidation.cs | ProcessAutomationSessionObservation | preserve result/call fallback |
| session path stats | ToolValidation.cs | ProcessAutomationSessionObservation | preserve path extraction |
| assistant response text | Concurrency/ToolValidation | ProcessAutomationSessionObservation | preserve latest assistant text selection |
| assistant error summary | Concurrency | ProcessAutomationSessionObservation | preserve existing parsing |
| execution log tool names | ToolValidation | ProcessAutomationExecutionLogObservation | preserve provider-native/internal trust rules |
| browser output files | ToolValidation/ArtifactValidation | ProcessAutomationExecutionLogObservation + ObservationSnapshot | preserve three source families |
