# Target Solution

## Design

Add a typed project-structure deferred completion service:

- The UI creates the canonical node immediately using `ProjectWorkbenchService.CreateObjectAsync`.
- The initial create request includes a generated placeholder image asset with the text `Waiting for Image creation by AI...`.
- The node receives explicit status/progress/metadata marking the completion as queued or running.
- The page enqueues a typed deferred completion request for `GeneratedImageAsset`.
- A background worker processes the request in a fresh DI scope, resolves the current provider profile, calls `IAgentImageGenerationService`, and updates the same node.
- `ProjectWorkbenchService` gets a focused media replacement method that saves new media and persists the node binding without changing node id, links, position, or parent.

## Generic User Stories

- Generated AI image: create a waiting image asset now, replace it when Comfy/OpenAI returns.
- Recording transcript: create a transcript node now, fill transcript/summary metadata when speech-to-text finishes.
- Document preview: create file node now, add thumbnail or extracted text when conversion/indexing finishes.
- Screenshot/browser evidence: create evidence node now, attach screenshot when browser capture completes.
- Process/workflow artifacts: create output nodes now, attach managed folders/files as runs finish.
- Repository/resource indexing: create resource node now, hydrate branch/path/metadata when the scanner completes.
- External storage copy: create node now, update route/storage reference after slow storage placement.
- AI enrichment: create node now, fill structured metadata after model analysis returns.

## Boundaries

- UI components orchestrate create/enqueue and patch visible state; they do not own long-running provider work.
- Application/workbench services own canonical node creation, media replacement, status, progress, and metadata.
- Provider runtime services own provider calls and Comfy/OpenAI contracts.
- Storage placement remains the single path for writing media bytes.
- Deferred completion requests are strongly typed; no stringly-typed command payloads.

## Canonicity Controls

- No client-only node identity.
- No delete/recreate to replace placeholder media.
- Node id, parent id, links, position, and object type remain stable.
- Completion failure updates the same canonical node with explicit failure state.
- Metadata overlay for deferred completion must be operational and must not count as a node-family payload.

## Performance Controls

- Provider calls do not block the Blazor event handler after the initial node is saved.
- Completion uses a bounded queue/worker or equivalent controlled execution path, not unbounded fire-and-forget component tasks.
- Normal success path updates one node and uses existing surface patching where available.
- Provider list refresh and dropdown behavior remain unchanged except for tests that prove prompt transfer.

## Minimal Edit Set

- Add strongly typed deferred completion request/queue/worker/processor under Workbench project-structure services.
- Add `ProjectWorkbenchService.ReplaceObjectMediaAsync` or similarly focused method.
- Extend metadata with a non-family deferred completion overlay.
- Change generated-image create to create placeholder node first, enqueue background completion, and patch completion/failure state.
- Update component/unit tests and browser validation.
