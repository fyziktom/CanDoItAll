# Assumptions And Risks

## Assumptions

- Existing runtime behavior is broadly valuable and must be preserved while responsibilities are extracted.
- The first implementation pass should not rewrite every MAF file; it should extract the most coupled seams behind typed collaborators.
- Some fallback creation currently exists to make tests and hosts easy to start. The refactor should distinguish intentional defaults from missing required services.
- Performance concerns are likely in startup/composition and provider attachment, not general request streaming or model latency.
- Browser validation is not required unless execution adds visible runtime diagnostics.

## Critical Path Risks

- The table below lists the critical risks that would invalidate the implementation sequence if not controlled.

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Treating partial-file movement as architecture | Produces more files without testable seams. | SB02 requires typed contracts and production callers before extraction. |
| Big-bang rewrite of `MafAgentRuntime` | High regression risk across all agents. | Subbundles split extraction by composition, provider/session/finalizer, feature drivers, tests, and performance. |
| Extracting interfaces with no real boundary | Adds boilerplate without testability. | Each new interface/collaborator must have a production consumer and direct tests or a real integration seam. |
| Removing fallbacks blindly | Breaks existing hosts/tests. | SB02 classifies required, optional, and defaultable dependencies before changing behavior. |
| Keeping fallbacks silently | Continues hidden misconfiguration and hard-to-debug failures. | Missing required services must fail explicitly with actionable diagnostics. |
| Measuring performance only after refactor | Cannot prove the refactor helped or did not regress startup. | SB01 records baseline measurement plan; SB07 captures before/after comparison. |
| Mocking too low-level implementation details | Brittle tests that freeze internals. | SB06 tests public/internal collaborator contracts and integration seams, not private method names. |

## Validation Risks

- The table below lists validation traps that must be blocked by proof, not by narrative claims.

| Risk | Required validation |
| --- | --- |
| Tests still use `MafAgentRuntime` plus reflection after extraction | SB06 must add direct collaborator tests and remove or reduce reflection dependency where touched. |
| A collaborator exists but production still uses the old private path | Critical proof must include Production Behavior Artifact Matrix and source assertions. |
| Behavior parity is assumed from passing compilation | Each extraction subbundle needs before/after tests around the moved behavior. |
| Performance proof relies on subjective startup perception | SB07 requires timed local composition measurements and external provider boundary separation. |
| Mockability is claimed without integration proof | SB06 must include tests using fake providers, fake runtime tool providers, fake context contributors, and fake workspace/MCP boundaries where applicable. |
| Silent fallback survives in new seams | Negative tests must prove missing required services fail predictably. |

## Reopen Triggers

- Reopen SB01 if implementation finds a major MAF responsibility not represented in the responsibility map.
- Reopen SB02 if a later extraction needs contracts that were not defined or classifies dependencies incorrectly.
- Reopen SB03 if capability composition cannot be tested without constructing full `MafAgentRuntime`.
- Reopen SB04 if provider/session/finalizer behavior remains coupled to private runtime state.
- Reopen SB05 if workspace, MCP, context, skill, or tool behavior still requires nested private runtime classes for direct tests.
- Reopen SB06 if integration tests still need private reflection for the extracted behavior.
- Reopen SB07 if startup measurements show an unplanned bottleneck or a regression introduced by extraction.

## Security And Data Risks

- Runtime tool access and approval behavior must not be weakened by extraction.
- Provider credentials and configuration diagnostics must continue to mask sensitive values.
- Fallback removal must not accidentally bypass dispatch gates, approval gates, or workspace scope boundaries.
- Test fakes must not become production fallback implementations.

## Scope Exceptions

- This bundle does not implement code.
- This bundle does not fix Financial Strategist, MarkItDown, document conversion, quotation extraction, margins, or project-structure writeback.
- This bundle does not require deleting all partial files in one pass; it requires real responsibility boundaries with proof.
