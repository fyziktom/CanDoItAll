# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Prompt textarea must reach provider. | `requirements/01-normalized-requirements.md#r1` | `subbundles/01-prompt-contract-and-provider-proof` | Component test records provider prompt; Comfy driver unit remains valid. | Source path is `ProjectStructurePage.ImageGeneration.cs`. |
| Provider/model/size/quality/format must reach provider. | `requirements/01-normalized-requirements.md#r2` | `subbundles/01-prompt-contract-and-provider-proof` | Component test assertions on `AgentImageGenerationRequest`. | Covers form input values. |
| Node appears immediately after save. | `requirements/01-normalized-requirements.md#r3` | `subbundles/03-generated-image-pending-node-flow` | Component test asserts node exists before delayed image service completes. | Must use canonical service create. |
| Waiting placeholder image. | `requirements/01-normalized-requirements.md#r4` | `subbundles/03-generated-image-pending-node-flow` | Component test and browser screenshot show placeholder image route/media. | Placeholder text must be in generated image content. |
| Same node receives generated image. | `requirements/01-normalized-requirements.md#r5` | `subbundles/02-generic-deferred-node-completion` and `03-generated-image-pending-node-flow` | Service/component tests assert stable node id and replaced media. | Critical canonicity proof. |
| Failure state is explicit. | `requirements/01-normalized-requirements.md#r6` | `subbundles/02-generic-deferred-node-completion` | Processor test or targeted component test with failing image service. | No silent fallback. |
| Generic deferred completion primitive. | `requirements/01-normalized-requirements.md#r7` | `subbundles/02-generic-deferred-node-completion` | Source assertion and service tests. | Uses typed completion kind. |
| ProjectWorkbenchService remains canonical boundary. | `requirements/01-normalized-requirements.md#r8` | `subbundles/02-generic-deferred-node-completion` | Source assertion and tests. | No JS-only or component-only persistence. |
| Avoid full reload in normal path. | `requirements/01-normalized-requirements.md#r9` | `subbundles/03-generated-image-pending-node-flow` | Component test with DB create counter where practical, plus source review. | Completion may be observed on reload if page is gone. |
| Rebuild/restart/browser validation. | `requirements/01-normalized-requirements.md#r10` | `subbundles/04-validation-and-browser-proof` | Build/test transcripts and Playwright screenshots. | Stop if ComfyUI connection blocks. |
