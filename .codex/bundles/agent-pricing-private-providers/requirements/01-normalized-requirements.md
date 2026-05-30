# Normalized Requirements

| ID | Requirement | Source |
| --- | --- | --- |
| R1 | Providers must expose an editable model price table with input, cached-input, and output token prices. | User note 1 |
| R2 | OpenAI provider defaults must be seeded from the official OpenAI API pricing page checked on 2026-05-30. | User note 1 |
| R3 | Ollama/private-style providers must have editable, non-zero realistic default prices. | User note 2 |
| R4 | Manual agent model overrides must be rejected unless the provider has a price entry for the override model. | User note 1 |
| R5 | Agent run metrics must calculate usage cost from token counts and provider model pricing. | User note 3 |
| R6 | Process live analytics and run history must use token-cost values when available. | User note 3 |
| R7 | Workflow-related run analytics must use the same pricing service where workflow execution exposes model usage. | User note 3 |
| R8 | Agent cards backed by private-style providers must show a `Private` badge. | User note 4 |
