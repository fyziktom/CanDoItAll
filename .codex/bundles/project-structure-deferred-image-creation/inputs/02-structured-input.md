# Structured Input

## User Goals

- Verify that the prompt textarea and image options are truly transferred to the image provider.
- Create generated-image nodes immediately instead of waiting for the provider.
- Show a waiting image placeholder while generation is still running.
- Replace the placeholder with the generated image when the provider completes.
- Make the mechanism generic enough for future project-structure nodes that need asynchronous completion.
- Protect project structure canonicity and performance.

## Direct Non-Goals

- Do not rebuild the provider dropdown behavior again.
- Do not make a UI-only fake node that disappears or changes identity after completion.
- Do not add a broad process/workflow dependency if a focused project-structure deferred completion service is sufficient.
- Do not hide provider errors by keeping a perpetual waiting state.

## Validation Targets

- Component/service tests for prompt transfer, immediate node creation, background completion, and same-node media replacement.
- Clean build for `src/CanDoItAll.Web/CanDoItAll.Web.csproj`.
- Restarted 5032 instance.
- Playwright proof through right-click node context menu: Assets -> Generate image -> prompt textarea -> provider -> create.
