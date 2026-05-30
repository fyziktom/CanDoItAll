# Current State

- Provider profiles currently store provider settings and suggested models, but no typed model-price table or private-provider flag.
- Provider extra settings already flow through JSON metadata via `AgentFrameworkProviderMetadata`, so pricing can be added there without a schema-breaking database migration.
- Agent run metrics store input and output token counts but do not store cached-input tokens or calculated cost.
- Process run `EstimatedCost` is seeded from target lead hours at a fixed rate and `ActualCost` is not synchronized from agent usage.
- Live process analytics aggregate persisted run costs and usage tokens, but they do not calculate live usage cost.
- `AgentSelectionCard` is the shared card surface for catalog and switch dialogs, so it is the right place for a private-provider badge.
