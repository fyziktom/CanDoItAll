# Assumptions And Risks

## Assumptions

- Browser proof should be required only when generic step contracts, expected artifacts, work briefs, project structure, or agent instructions require runtime or browser evidence.
- Provider-native browser MCP tool calls can be inspected from at least one durable source: tool receipts, execution logs, tool results, or session state. The implementation must prove which source is authoritative.
- Some existing process definitions already contain browser proof language. Changes may need seed/template updates and migration or synchronization guidance for existing development definitions.
- The correct fix is not to ban quality acceptance when a screenshot path is under `.playwright-mcp`; the fix is to import or mirror that file into scoped process artifacts and validate it.

## Risks

- Overfitting to Tetris would make the process brittle. Mitigation: runtime enforces proof categories and artifact existence; project structure and step definitions provide domain acceptance details.
- Over-strict console handling could fail valid runs after intentional host shutdown. Mitigation: record active proof window and classify post-stop disconnect noise separately.
- Copying provider-native files without validating them could turn weak evidence into official evidence. Mitigation: require content checks, non-empty screenshot checks, and semantic proof assertions.
- Tests that seed artifact rows directly could miss the production emitter. Mitigation: critical tests must exercise the production projection/validation path.

## Critical Path Risks

- `SB01` is the critical foundation. If browser MCP evidence cannot be projected into process artifact records, later proof gates can still be bypassed by prose.
- `SB02` depends on `SB01`. If the runtime proof gate checks only text or counts, it will not catch invisible gameplay, missing screenshots, or detached console diagnostics.
- `SB03` depends on `SB01` and `SB02`. If process definitions require exact artifacts before the storage/projection path works, valid runs may be blocked without recoverable proof instructions.
- `SB04` depends on every earlier subbundle. A live-process pass is not meaningful unless the previous gates can reject the original failure shape.

## Validation Risks

- A test that asserts only that a markdown evidence pack mentions "screenshot" would preserve the current defect.
- A test that uses manually seeded `Processes_ArtifactRecords` would not prove provider-native MCP evidence ingestion.
- A browser screenshot that is captured but not reviewed against explicit interaction questions would preserve the shallow-pass trap.
- A console test that treats all disconnects as fatal would create noise after intentional app stop and make the process impractical.

## Reopen Triggers

- Any UI/browser QA step completes with a screenshot requirement but no image artifact record under the scoped process run artifact root.
- Any evidence pack cites `.playwright-mcp` paths without corresponding process artifact records.
- Any completed QA or release-readiness step claims zero console errors while the captured active proof interval contains errors or unclassified warnings.
- Any process accepts an interactive UI proof without a representative interaction assertion tied to project structure or step evidence requirements.
- Any "generic" implementation adds product-specific checks such as Tetris piece names, board dimensions, or game rules to process core.
