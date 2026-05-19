# Current State

## Initial P0 Audit

- `CognitiveMemoryAdvancedServices.cs` contains several independent public services in one large file: probe, self model, calibration, self-regulation, professor review, answer gate, epistemic drive, cross-project promotion, distributed compute, and score trace support.
- `CognitiveMemoryApi.cs` contains endpoint mapping, helper mapping methods, request DTOs, and response DTOs in one file.
- `CognitiveMemoryRecallServices.cs` is a single large orchestrator with candidate loading, lexical/vector/workspace/signal/graph channels, scoring, context rendering, persistence, and nested snapshots in one file.
- `CognitiveMemoryPage.razor` and `.razor.cs` remain broad operator surfaces. Any UI split needs component-test and browser proof.
- Projection lifecycle service exists, but the product path to rebuild stale projection records is missing.
- Automation schedule settings exist, but no explicit Cognitive Memory automation execution service was found.
- `CognitiveMemoryAgentContextContributor` already integrates with MAF but combines recall result diagnostics with agent context text and skips unavailable memory in interactive paths.

## P0 Direction

- Start with low-risk file decompositions that preserve behavior.
- Add explicit service/API paths for projection rebuild and automation execution instead of hidden background mutation.
- Add tests around new behavior before claiming P0 completion.
- Keep large UI decomposition conservative unless tests/browser validation can cover the change.
