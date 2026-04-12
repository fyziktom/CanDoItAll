# Target Solution

## Strategy

- Use `PageScaffold FillHeight="true"` on the processes workspace and wrap the page body in a `Stack` with `flex-1 min-h-0` so summary tiles stay at the top while `ListDetailShell` consumes the remaining height.
- Keep `ListDetailShell` as the selection shell. Add pane classes only where needed and move scrolling responsibility into explicit list/detail wrappers instead of letting the document scroll.
- Configure the detail `Tabs` as a real workspace-height segmenter by enabling `FillHeight`, panel overflow, and a bounded root class.
- Let the fullscreen templates dialog use the dialog body as the available height source. Replace the extra fixed viewport wrapper with a `h-full min-h-0` layout so the list/detail shell owns the internal scrolling.
- Tighten the Mermaid preview viewport with a bounded host container that clips transformed content without introducing a new rendering abstraction.

## Boundaries

- No new layout service, no new shared component, and no component-library refactor in this bundle.
- No fallback behavior that silently changes zoom math or hides rendering errors.
- No widening of scope into unrelated process-canvas or template-data work.
