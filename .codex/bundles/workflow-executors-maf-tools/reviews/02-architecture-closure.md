# Architecture Closure Review

## Accepted Decisions

- Executors are a workflow node capability, not a special case inside individual UI components. Saved definitions carry `WorkflowExecutorId`, strongly typed settings JSON, input/result shapes, and execution policy.
- Runtime dispatch is centralized in `IWorkflowExecutorInvoker`; timeout, retry, and explicit failure semantics are not duplicated in each executor.
- Built-in executor descriptors expose `SetupRendererKey` and default settings now. That is the right plugin seam: a future plugin can provide descriptors, implementations, and setup renderer components without changing the persisted node contract.
- ClosedXML is isolated in `CanDoItAll.Tools.Documents`. No workflow or UI project should depend on ClosedXML types directly.
- Planned generic tools are catalogued but disabled in the toolbox. Persisted references fail explicitly at runtime through `PlannedWorkflowExecutor`.

## Review Findings

- The initial DI implementation only registered executors in the standalone hosting extension. Browser prerender exposed that the app uses `AddAgentFrameworkModule`, so the module service registration had to be patched with scoped executor/catalog/invoker services.
- The image executor cannot be honestly completed without extracting the existing provider bridge from the MAF runtime image tools. Shipping it as an explicit runtime error is safer than inventing a second provider path.
- Project-structure executor has the right service boundary but still needs seeded integration coverage for live asset creation.

## Follow-Up Work

- Extract an `IWorkflowImageGenerationService` or equivalent provider bridge from the existing image-generation path and inject it into `ImageGenerationWorkflowExecutor`.
- Add integration tests using a real seeded project to cover `ReadTree`, `ReadNode`, and `CreateAsset`.
- Introduce plugin registration once the app has a general plugin loader; keep plugin contributions constrained to executor descriptor, executor implementation, and setup renderer registration.
- Add durable-run proof once the production DurableTask/Azure Functions workflow host is wired for these executors.
