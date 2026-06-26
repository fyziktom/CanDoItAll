# Assumptions And Risks

## Assumptions

- The prompt-transfer suspicion is likely caused by either provider output behavior or the lack of end-to-end project-structure test coverage, not by a missing field in the current request object.
- Local ComfyUI can remain a validation target, but automated tests should not require a live ComfyUI server.
- A real placeholder media asset is acceptable because it gives the existing canvas, selection panel, and preview paths an image route without special-casing empty media.
- In-process deferred completion is acceptable for this feature if the canonical node records the pending/error state. A future persistent job table can be added if restart-resume becomes a hard requirement.

## Critical Path Risks

- Updating media on an existing node touches canonical storage binding logic. A weak implementation could drift route/media/storage reference state from the object.
- Running provider calls from a Blazor component would risk disposed scopes and duplicate work. Completion must run through a service that owns fresh DI scopes.
- Adding generic metadata to `ProjectObjectMetadataEnvelope` must not violate the existing single-family validation rule for typed node metadata.
- Full graph reloads after every completion would hurt performance on large project structures.

## Validation Risks

- bUnit does not automatically start hosted services. Deferred completion tests must either start the worker explicitly or test the processor directly.
- Browser proof must use the same right-click context menu path the user uses, because direct component invocation previously missed UI timing issues.
- Local ComfyUI may return visually poor output even when the prompt is transferred. Proof must include request payload/assertions, not only subjective image quality.

## Reopen Triggers

- The generated image request seen by the provider does not contain the prompt textarea text.
- The created node ID changes between placeholder and completed image.
- Placeholder nodes are created client-side only or disappear on reload.
- Completion requires a full structure reload in the normal success path when a single-node patch is possible.
- Provider failure leaves the node permanently marked as waiting without actionable status.
