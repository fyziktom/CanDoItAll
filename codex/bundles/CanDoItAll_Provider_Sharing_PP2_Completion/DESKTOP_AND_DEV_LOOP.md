# Large desktop and development loop

Primary viewport: 1920x1080, 100% zoom. An optional second large viewport is permitted only to
resolve a concrete defect. No small/medium/mobile/tablet tuning or new responsive matrix.
Do retain keyboard/focus, actual visibility, bounded scroll regions, dialogs, sticky actions,
content truncation and complete menu geometry. DOM presence alone is not visible usability.

PP1 reported a smaller independently runnable graph, but repeated sandbox Razor hot reload stopped
showing the second sample. Its successful final samples used automatic rebuild/restart, not hot
reload. S0 does not claim to have closed this. Preserve that historical distinction.

Before final measurement inspect launch settings, output paths, static-asset manifests and SDK;
never run tests/builds/watchers against the same mutable output concurrently. Establish a clean
source-pair reproduction. Make three reversible Razor, C# and scoped CSS changes as applicable;
observe delivered markup/version/asset content rather than only a console success line. Track
startup, hydration, detection, compilation and visible update separately, restore every source hash.
Keep the probe bounded. A proved local asset/project issue can be repaired; a larger tooling issue
gets its exact minimal reproduction and limitation, not an unbounded SDK/framework rewrite.

Report methods separately (hot reload vs restart), host/configuration/sample order and variability.
Do not claim universal Web speedup or compare unlike methods as a percentage improvement. A working
restart path remains useful but does not demonstrate that repeated hot reload works.
