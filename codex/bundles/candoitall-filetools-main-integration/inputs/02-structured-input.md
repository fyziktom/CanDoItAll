# Structured Input

## Requested Outcome

Integrate FileTools browsing and interaction into CanDoItAll through a storage-first, security-aware, test-gated sequence that begins with one project-files pilot and grows only after real browser proof.

## Literal Constraints

- `must prepare bundle only` -> preparation changes only `bundle://`.
- `first point is improvement of Storage Driver` -> `SB02` is the first production-code phase.
- `proper testing and when working fine UI can start` -> all UI depends on the `SB09` backbone gate.
- `test it on one case like search of project files` -> `SB10` owns exactly one end-to-end pilot before broader UI.
- `subbundles contain all necessary information` -> every README contains owners, sources, contracts, tests, negative cases, progression, and reopen rules.
- `review/refactor/architecture cleanup after phase` -> `SB05`, `SB09`, `SB11`, and `SB17` are hard gates.
- `large screen desktop only` -> `1900x1200` primary, `1440x900` minimum; no smaller breakpoints.
- `lots of files` -> provider work, metadata probes, search, retained state, bytes, and rendered rows are bounded and measured; page-size-only proof fails.
- `asset nodes ... open them in dialog on doubleclick` -> preserve current Project Structure image/PDF double-click and dialog lifecycle.
- `without filebrowser when it is opening just one file` -> direct typed known-file FileInteraction path with zero browser initialization.
- `filebrowser ... only ... browsing of multiple files` -> distinct typed collection browsing intent; never infer from a path/string/count.
- `remove bundles from gitignore` -> repository `.gitignore` no longer ignores `codex/bundles/**`.

## Named User Stories Preserved From Legacy Evidence

- Search/browse filtered project files and project/subproject aggregates.
- Open files from a project card dialog.
- Browse project structure and folder-node scopes in a compact floating window.
- Browse managed/output/product files from process-run history.
- Browse project, filesystem, IPFS, and FTP sources in Resources and promote an authorized item to a resource.
- Open known files through FileInteraction and later migrate supported preview/edit/save flows.
- Preserve direct known-asset dialogs while keeping collection discovery in FileBrowser.
- Keep live agent/process folders uncached while allowing explicitly configured aggregate/IPFS caching.

## Success Bar

No downstream phase may rely on compile-only or screenshot-only evidence. Storage and integration foundations require direct isolated tests, meaningful negatives, dependency/cycle proof, composition smoke, and source assertions. UI requires real desktop browser behavior plus inspected screenshots and console/network checks.

## Stop Rules

- Stop on missing FileTools package provenance, stale repo anchors, an untrusted prerequisite, project cycles, security ambiguity, provider capability lies, or an unavailable browser/component/watch tool required for the phase.
- Do not work around a failed authority check with an unsigned token, absolute path, default provider, or client-side flag.
- Do not start broader UI because the pilot merely renders; the pilot must prove authorized search, activation, content load, negative access, and desktop layout.
- Do not accept paging when the provider still performs an unbounded enumerate/sort/hash before returning page one.
- Do not route a known Project Structure asset through FileBrowser to reach FileInteraction.
