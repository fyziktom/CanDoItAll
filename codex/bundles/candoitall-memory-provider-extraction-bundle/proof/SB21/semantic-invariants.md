# SB21 Semantic Invariants

## Query And Context Pack UI

- The generic `/memory` UI builds query requests through `IMemoryOperationHandler`.
- A sync provider result renders handler status, operation id, requested capability, context pack id, confidence, feedback handle, section text, and citations/source references.
- Query actions remain disabled when no provider is selected, the provider is disabled/unhealthy, or the selected provider lacks the requested query capability.

## Operations, Events, And Feedback

- Operation rows are loaded from the generic operation ledger by selected provider id and expose operation kind, id, status, status reason, and requested capability.
- Operation refresh and cancellation controls are available only when the selected provider explicitly supports `operations.status`.
- Event inbox rows are loaded from the generic event ledger and acknowledgements enqueue through the shared handler path.
- Feedback submission is tied to a delivered context pack id, supports explicit feedback stage selection, and chooses immediate or delayed feedback capability based on the selected stage.

## Manual Ingestion

- Manual ingestion uses `ManualMemorySourceIngestionService`, which captures a source snapshot through `IMemoryOperationHandler.CaptureSourceForIngestionAsync`.
- Manual ingestion actions are disabled unless the selected provider is enabled, healthy, and declares `ingestion.snapshot`.
- Captured ingestion operations are visible in the same generic operation ledger as query operations.

## Boundary

- The generic Memory UI does not reference native Cognitive Memory pages/services, Qdrant, OpenAI, or RAG provider implementations.
- Browser proof disables Qdrant explicitly in the test host and creates providers only through visible UI actions.
