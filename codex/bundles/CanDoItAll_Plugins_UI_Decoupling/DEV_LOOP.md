# Development-loop evidence

The objective is a usable small Plugins sandbox and a demonstrably real dependency cut, not a promise that splitting a Razor assembly automatically accelerates the full application. The full Web host may still watch/build a broad graph.

## Record the environment before moving the renderers

Record actual main/sibling commits, branch and local changes, SDK/MSBuild/runtime, operating system, dependency source/package mode, configuration, relevant machine/load conditions and restored/cold cache state. Use an isolated output configuration such as `PluginsUiProof` through the repository's supported mechanisms; keep the running user's binaries and process untouched.

Measure the existing `/plugins` production route before extraction. After extraction measure the same production route and the representative complete Plugins sandbox. The earlier TestLab measurements belong to a different surface and are not a Plugins baseline.

## Four distinct evidence sets

| Evidence | Required observations |
| --- | --- |
| Evaluated build graph | Actual project references after props/targets and source-mode package replacement, unresolved edges, cycles and root-inclusive count |
| Package/assets | Transitive packages, native runtime assets, static web assets, CSS/font/JS/icon paths, public type closure |
| Runtime/render closure | Real six-section workspace, package dialog and settings controls, actual startup services, no production owners in sandbox |
| Watch/edit behavior | Actual watch list and runtime logs; startup-to-interactive form; visible source/asset changes without stale-binary shortcuts |

Microsoft's dotnet-watch documentation describes recursive project-reference inspection [W02 in SOURCES.md](SOURCES.md). Evaluate actual targets rather than treating literal csproj text or a parent injection scan as sufficient proof.

Useful inventory commands, adjusted to the actual created project paths and installed SDK:

```powershell
dotnet watch --list --project src/App/CanDoItAll.Web --configuration PluginsUiProof
dotnet watch --list --project src/Sandboxes/CanDoItAll.Plugins.UiSandbox --configuration PluginsUiProof
```

Inspect restore `project.assets.json`/dgspec and evaluated reference metadata. Parse enough to detect unresolved edges; do not count only filenames in `src/UI`. Explain allowed abstractions reached through Plugins.Abstractions/AgentFramework.Models. A source-mode dependency on concrete plugin/runtime implementation is a failure even if its API happens not to be exercised in one scenario.

## Asset parity and distribution

Use actual BaseLib components and required registrations. Include the production theme and real CSS/fonts/scripts/icons. Linking the existing application stylesheet as a content/static-asset input is acceptable; referencing the whole Web project just for CSS is not. Show the authoritative asset source and how it is watched/built. Do not copy a stale generated CSS file without a rebuild path or silently change parity to a simplified theme.

The reviewed Plugins Pages subtree has no feature scoped-CSS file, but the real asset closure still includes icon sources and shared assets. Inspect current descendants and package/static icons. If any feature CSS/JS is added or moved, measure it and preserve supported source/publish asset paths. Inspect the dialog overlay and both package/static icon cases using shipped harmless fixtures rather than network-dependent images. Do not add a special fast-mode build framework unless needed and justified; a correct Parity sandbox is sufficient.

## Comparable measurements

Use the same representative plugin/settings shape, viewport and restored-cache conditions for comparisons. Keep unrelated background workload recorded; avoid concurrent hosts contending for the same output directories. Capture at least three edit samples for each applicable source kind when practical, showing all samples and a median, not only the fastest.

| Kind | Probe |
| --- | --- |
| Startup | Start owned host/watch process; observe actual interactive selected form and loaded required assets, not merely listening port |
| Razor | Change a visible label/expression in an extracted Plugins renderer; verify changed DOM |
| C# | Change a small actually executed presentation/helper expression; verify new output and hot-reload/restart classification |
| CSS | Change the authoritative theme/feature stylesheet and observe computed style, then restore exactly |
| JavaScript | Only if present/changed in this slice; verify executed new behavior and module/cache path |

Record unsupported hot-reload edits or process restart separately from in-place patches. The watch set may be supplemented by static-asset runtime watching; demonstrate actual file-update behavior instead of claiming a missing line in `--list` proves an asset is not watched. Mark missing/not-applicable measurements honestly and justify them. A single smoke observation is not a performance benchmark.

Do not switch to stale package binaries, suppress required watching, disable important build targets or omit heavy child renderers to obtain a better number. Full-Web regressions deserve explanation, but a slower noisy full-Web sample is not automatic proof that a useful independent sandbox failed its purpose.

Keep raw logs, samples and screenshots in task-owned ignored evidence paths with sanitized environment details. Restore all measurement edits byte for byte and stop only owned watcher/browser processes. Update the module boundary record with the graph, startup/edit observations, sample conditions and limitations.
