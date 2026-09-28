# Development-loop and asset proof

The main objective remains a small, representative UI development host. A new RCL
alone does not prove that `dotnet watch` on the entire Web graph becomes faster.
Measure the actual boundary and report the sandbox benefit separately. [EV09, EV12]

## Baseline

Finish S0 first. Record its local SHA or exact uncommitted patch state, SDK/OS,
configuration, source/package mode, sibling revisions, and relevant process load.
Then establish the pre-Memory-extraction Web baseline on a task-owned isolated host.
Never use or restart the ordinary app/database or somebody else's watch session.

Use a harmless representative Memory scenario: at least two profiles with different
supported driver/health states, profile editor, bounded operation/context records,
and a registered safe Provider UI panel. Real executable query proof is separate
from synthetic ledger fixtures. Record what is seeded and which owners are real.
The post-change Web and sandbox measurements should use comparable visible content;
report differences instead of pretending they are identical runtimes.

## Three independent inventories

| Inventory | Record |
| --- | --- |
| Evaluated build/runtime closure | Root projects, transitive project/package references, source replacements, native/runtime assets, unresolved/cyclic edges |
| Render/effect closure | Real components, dynamic provider extension slot/registration, required DI, transport/worker exclusions |
| Asset/watch closure | Razor, executed C#, scoped CSS/theme, static assets and any actually used feature JS/extension assets |

Measure the real MSBuild/NuGet graph, not a recursive text search for ProjectReference.
The source-mode replacement in repository targets matters. Package-only baseline
versus source-mode final output is not a fair comparison. Keep the base feature
closure separate from optional third-party provider components.

Use the current CLI's actual command syntax, for example:

```text
dotnet watch --list --project <actual-project> --configuration <isolated-configuration>
dotnet watch --non-interactive --project <actual-project> --configuration <isolated-configuration> --no-launch-profile -- --urls <owned-loopback-url>
```

Record exit codes and actual watch inputs. A linked theme stylesheet can be a content
input without being a Web project edge. Confirm how the installed tool watches and
serves it rather than equating a missing literal list entry with a missing asset.

## Edit observations

Warm restore/build consistently. Keep startup-to-interactive separate from
edit-to-visible change. Collect multiple observations for meaningful comparisons;
report all samples and medians where appropriate. A single successful smoke is a
single sample, not a statistically established improvement.

Probe real paths: a child Razor label, C# presentation logic actually executed by
the workspace, and a computed scoped-CSS property. Confirm the marker visually or
through browser DOM/computed style, not only the watch console. For feature JS,
first inventory whether it exists. Do not add artificial production JavaScript to
satisfy a measurement table; report not applicable when appropriate. Browser
refresh/restart requirements must be recorded rather than hidden in the timing.

Measure original Web, extracted Web and standalone sandbox separately. Keep read
budgets and memory/draft retention checks alongside latency: a smaller project
graph is not a justification for a provider request on every keystroke. Count
passive versus explicit owner operations and confirm bounded scenario collections.

## CSS and dynamic surfaces

The existing Memory scoped stylesheet must follow the actual owning DOM. If one
page becomes multiple Razor components, scope IDs and `::deep` anchoring change.
Do not fix this by injecting broad global selectors or copying a second theme.
Check accepted desktop geometry, focus, tab remount, scroll and safe provider
surface rendering. [EV13]

Build/publish the sandbox and run the published host without a database or runtime
configuration. Verify actual stylesheet/script/font/local-image and fixture URL
responses, computed styles, registered RCL parameters and blocked URL states.
Record optional external extension assets honestly. No live remote account or
third-party site access is required for this proof.

## Receipt and cleanup

Record original/extracted/sandbox source identities, exact commands, inventories,
sample conditions, successes and failures. Restore every temporary probe edit
byte-for-byte. Stop only owned process handles/containers after matching captured
ownership. Keep raw logs/screenshots/measurement JSON in the repository's ignored
evidence location and summarize durable facts in the maintained boundary document.
Do not reuse the historical Scheduler numbers as Memory measurements. [EV05]
