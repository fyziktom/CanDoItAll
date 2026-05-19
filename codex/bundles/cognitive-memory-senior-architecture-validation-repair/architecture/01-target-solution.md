# Target Solution

## Technical Direction

Treat the previous completion claim as mostly valid but not sufficient. The architecture bundle and LB4U follow-up are structurally complete; this pass focuses on evidence-backed repairs in the hot memory surfaces that decide what agents actually receive.

## Boundaries

- Keep durable memory authority inside `CanDoItAll.Modules.CognitiveMemory`.
- Keep HTTP route contracts in `CanDoItAll.Web\Api\CognitiveMemoryApi.cs` unchanged unless a test proves the contract is defective.
- Keep query repairs inside existing services rather than introducing new abstractions.
- Leave large-file decomposition as follow-up debt unless the current repair requires a split for correctness or testability.

## Repair Shape

- In recall lexical activation, limit ordered candidate rows in SQL before projection/materialization.
- In signal querying, apply recency and access filters before ordering and taking the requested page.
- Add targeted regression coverage around the signal query ordering bug.

## Memory Quality Validation Shape

- Start or reuse the CanDoItAll web app.
- Check `/api/access/status`, `/api/cognitive-memory/status`, and `/api/cognitive-memory/settings`.
- Use API ingestion/recall/probe surfaces where available to validate source-backed context.
- If live provider/database state blocks full validation, record the exact failing endpoint or startup diagnostic.
