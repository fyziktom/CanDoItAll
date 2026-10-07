# Development-loop proof

The objective is a small, usable Files development host, while preserving the already-small P1 host. It is not uniformly faster whole-Web builds.

Capture the baseline before changing P2 rendering: application/Components/FileTools revisions, dirty-file hashes, SDK, source/package mode, exact configuration, evaluated project and package closure, `dotnet watch --list` inputs and an owned-host startup/hydration observation. Use a short owned source path if a comparison checkout is required. Never change the user's normal host or discard unrelated edits.

Afterward measure the same conditions for the new Files sandbox and changed Web, and verify the existing P1 graph/watch set has not acquired the Files dependencies. Count actual evaluated references and assets rather than reading only the root csproj. Keep a production CSS content link distinct from a project reference.

For each applicable Razor, C#, scoped-CSS and feature-owned JS edit, collect three first/warm-labeled edit-to-visible observations, with browser interactive readiness. Do not add JS solely to create a benchmark scenario. Retain failed/stalled probes separately, disclose polling vs normal watcher and concurrent validation load, and restore every changed probe byte with hashes. Do not present a median-only number that hides an initial rebuild or claim no backend rebuild without observing it.

Independently publish and run the sandbox outside its source directory; verify actual stylesheets/fonts/viewers and visible content. A package source file existing on disk is not proof it was served. Do not rerun 37 older complete browser campaigns for this measurement. No small/medium screen work: see [desktop policy](LARGE_SCREEN_POLICY.md).

The P1 report observed a 300-path sandbox watch set, but also a slow first Razor sample (R02). Retain that honest distinction. Produce a useful documented command for the new Files sandbox and explain which edit types trigger Hot Reload, restart or rebuild on the actual SDK.
