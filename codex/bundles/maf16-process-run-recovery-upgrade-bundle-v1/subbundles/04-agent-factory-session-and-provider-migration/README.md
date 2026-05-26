# SB04: 04-agent-factory-session-and-provider-migration

## Goal

Migrate `MafAgentRuntime.AgentFactory` to MAF 1.6 APIs.

## Required work

- Fix compile errors around `AIAgent`, `ChatClientAgentOptions`, `AsAIAgent`, `AIContextProviders`, chat history/session persistence, and provider adapters.
- Verify OpenAI Chat Completions, OpenAI Responses, Azure OpenAI, and Ollama still build agents.
- Check stored-output-disabled Responses path and reasoning encrypted content behavior.
- Add adapter-level tests for non-streaming and streaming execution if both are supported.
- Preserve execution run metadata and context contribution trace capture.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB04` are updated and the next subbundle can safely depend on it.
