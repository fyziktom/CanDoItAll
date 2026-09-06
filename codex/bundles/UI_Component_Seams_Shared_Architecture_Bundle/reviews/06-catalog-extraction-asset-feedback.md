# Proven feedback from catalog rendering extraction

Evidence: CDA-UI-SEAMS-CATALOG-01 SB01, SB02 and the completed SB03 comparison. Exact measurements and their limitations remain owned by that child.

1. A controlled rendering boundary can move independently when its complete dependency closure is known. Keep effect hosts and production composition outside the rendering assembly. Audit both transitive references and existing reverse consumers; a short direct-reference list is insufficient.

2. Physical UI portability includes the actual child components, CSS isolation, fonts, icons, tooltips and generated theme assets. A sandbox that substitutes these does not establish production rendering parity.

3. Verify generated asset bytes and computed browser styles, then a real edit/undo cycle. HTTP success alone is insufficient: a linked asset can be discovered at build time yet served from the wrong physical content root during development.

4. Readiness evidence must identify the intended live runtime and a working interactive UI. An HTTP response or prerendered markup alone does not prove the watch generation or event-handling circuit is ready.

5. Preserve a pre-move baseline and the same representative rendering projection across hosts. Separate process-cold startup with populated caches from clean-build time and warm editing. Freeze patch, visible predicate, repetition, asset pipeline and failure classification before comparison.

6. Distinguish developer-observed edit-to-visible latency from SDK compilation time. Orchestration and screenshot tools can affect observation; retain failed calibration and incomparable trials explicitly rather than silently substituting a favorable timing or screenshot.

These are dependency, asset and evidence rules. They do not make a small project graph a performance result, require a generic sandbox framework, or expand the extraction boundary to effect-heavy editors.

The executed checkpoint demonstrates why SDK apply time and observed browser latency must remain separate: lower update work did not establish a general warm-edit improvement in the managed observation loop. A valid negative or mixed result closes the measurement requirement without inventing a performance benefit.
