# Target Solution

## End State

- The repo has a durable, regenerable inventory of current API routes, DTOs, docs coverage, skill coverage, and agent runtime tool parity.
- Docs and skills tell the same story as the code: exact routes, current DTO fields, preferred Cognitive Memory v1 base path, provider capabilities, and documented HTTP-only exceptions where runtime tools are intentionally absent.
- API tests and drift guardrails make future route changes visible instead of relying on manual review.

## Boundaries

- Source route and DTO types remain the contract authority.
- API docs and skills should be generated or checked from source where practical, but implementation should stay small and avoid building a large documentation platform.
- Runtime tool additions must cross the same boundaries as existing MAF tools: descriptor, request DTO, service call, policy constant, approval behavior, and test coverage.
- Active skill sync is a release step, not optional cleanup.

## Allowed Side Effects

- Markdown docs and repo skill edits.
- Focused API and runtime-tool code changes where parity requires new calls or route assertions.
- Focused tests and validation scripts.
- Generated inventory workbook and proof artifacts under this bundle.

## Notable Repair Decisions

- Cognitive Memory should prefer `/api/cognitive-memory/v1` in new examples while documenting legacy compatibility.
- Process and Project Structure runtime tool gaps must be resolved explicitly; silent HTTP fallback in agent behavior would hide missing capability.
- Plugin and Projects API skill coverage needs a recorded decision before final closure.
