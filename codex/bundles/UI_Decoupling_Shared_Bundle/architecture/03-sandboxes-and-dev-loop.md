# Independent host, real assets and measurable development speed

A sandbox uses the real renderer, children, dialogs, canvas, charts and file/conversation presentation. It supplies deterministic scenario data and narrow adapters. It must start without production DI, database, vault, model credentials, runtime workers or fake registrations leaking into the application. A native mock switch in an otherwise heavy production module is not an independent host.

Preserve a meaningful browser-unavailable state for desktop-only effects. Do not execute an external app or use real microphone credentials to make a sandbox attractive. A deterministic adapter may simulate an effect result while the real button/dialog remains; native effect proof is separate.

Scenarios cover representative data, loading/empty, large bounded data, dirty and raw invalid fields, busy/conflict, partial failure/retry, removed/recreated target, restrictions, nested dialogs and independent simultaneous surfaces as relevant. They must exercise the full selected family, not only a read-only overview. Scenario-only fault injection belongs in test/development composition, never in production fault endpoints or weakened permission switches.

## Preserve both dependency and asset modes

Inspect current `Directory.Build.props`, `Directory.Build.targets`, package properties, package scripts, sandbox READMEs and CI. Do not assume old sibling SHAs or property names. Source/package dependency mode and Fast/Parity asset mode are independent axes. Source mode is the normal local development path where configured. A sibling root being required for evaluation does not mean every one of its projects is in the sandbox closure.

Move scoped CSS, JS modules, imports, namespace assumptions, static-web-asset paths, Tailwind inputs, route discovery and registrations together with rendering. Verify source and published startup using production-equivalent assets. An old generated CSS file from another checkout is not reproducible delivery. Parity may consume a content-only link to production theme output without compiling Web. A Fast mode may narrow asset work only with documented differences and actual full-surface coverage; it is not the sole parity proof.

Keep build/runtime asset mode aligned and fail explicitly when required assets are missing. Preserve chart/canvas JS, maximization/body-lock cleanup, scroll/focus behavior and real modal stacking. A fake SVG or screenshot of a placeholder is not proof of those components.

## Measure, do not promise

Extracting a leaf does not automatically accelerate `dotnet watch` of the full Web dependency graph. The independent host is the principal lighter loop. Record three comparisons where applicable: original versus changed Web (regression), changed Web versus representative sandbox (practical benefit), and Parity versus Fast (asset trade-off).

Record machine/OS/SDK, app and sibling revisions, relevant dirty delta, source/package and asset modes, actual launch/watch commands, evaluated recursive references, watch list, cache state and process reuse. Measure startup and save-to-visible change for Razor, C# presentation logic, scoped CSS and JS when present. Distinguish hot reload, browser refresh, process restart and missed update. Several warm samples and an explicit cold sample are preferable to a percentage from one observation; report actual counts/range rather than imposing a machine-independent millisecond target.

Use the current SDK's `dotnet msbuild` evaluated item/property queries and `dotnet watch --list` as appropriate, with current sandbox properties. Direct item output is not the full transitive graph. Record unresolved edges; use binlogs/resolved restore assets for ambiguity. Do not use Watch=false, missing references, disabled features or hidden source edits as a fake dependency reduction.

Use isolated output configurations, task-owned ports/processes and reversible measurement edits. Restore only owned changes without reset/cleaning user work. Any later change invalidating the tested asset or runtime candidate requires an affected rerun or a stale label. “Not measured” is an honest status; guessed speedups are not.
