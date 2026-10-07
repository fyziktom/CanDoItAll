# Development-loop proof for TestLab

This protocol defines future measurement; it contains no reviewer-measured performance numbers. Follow the current repository's source-dependency/asset rules and [shared development-loop guidance](shared/architecture/03-sandboxes-and-dev-loop.md).

## Compare the right hosts

Record three separately labeled observations where feasible:

| Host | Purpose |
|---|---|
| Actual pre-change Web host, owned isolated instance | Baseline for editing TestLab in the existing full application |
| Post-change Web host under comparable conditions | Production compatibility and any full-host loop change |
| New TestLab sandbox in Parity mode | The intended lightweight day-to-day renderer development loop |

A new sandbox has no historical before-version. Comparing it with the full application is a workflow comparison, not a same-host microbenchmark. Do not call smaller project/watch counts a measured latency improvement. Microsoft states that dotnet watch scans the project-reference graph; extracting an RCL does not automatically remove that graph from the full Web process. [E04](SOURCES.md#e04)

## Provenance and samples

Record application/sibling SHAs, local changes, SDK, OS, machine summary, source/package mode, build configuration, asset mode, browser/viewport, database isolation and cold/warm cache conditions. Separate first restore/build from normal iteration. Use several repeated comparable edit samples when feasible; retain individual samples, sample count and median/range rather than only a favorable observation. Fewer or missing samples must be labeled, never fabricated.

Record startup-to-render and edit-to-visible behavior for a Razor markup edit and relevant C# UI change. Also cover scoped/application CSS or JS if the actual extracted closure owns/uses them. Do not add dummy JS merely to satisfy a checklist. Report not-applicable with the closure inventory when appropriate. Revert only the measurement edits you own. Capture whether each event was a hot reload, build/restart or browser refresh.

## Graph, assets and instrumentation

Capture evaluated project references under the actual source mode, transitive package/native runtime assets and watched file paths. Do not rely only on raw csproj text, `using` scans or assembly names. `Directory.Build.targets` can replace Components package references with sibling project references. Use current SDK/MSBuild-supported commands and retain their exact invocation/output.

`dotnet watch --list --project <actual-project>` is a useful watch-set observation; use the same property/configuration inputs as the corresponding development launch. Check `dotnet watch --help` for the checked-out SDK rather than carrying forward incompatible argument ordering. The output must include relevant feature Razor, C#, styles and scripts while excluding unrelated backend modules from the sandbox's graph.

Prove Parity mode visually and through successful asset requests. A CSS file linked as content is legitimate without a compile reference to Web; assets need to reach the browser, not only exist on disk. Check Tailwind input coverage for moved markup and static web asset/isolated CSS paths. Do not copy shared component implementations, disable watching required source, downgrade source mode to stale packages, or change the SDK to manufacture a result.

## Safety

Never stop the user's app, kill all dotnet processes, change production ports/configuration, reuse ordinary data or remove unrelated containers. Use an owned test fixture/port and supported isolated outputs. Source-mode sibling edits can rebuild broad graphs; record that rather than suppressing the watcher. If the environment prevents a before/after observation, say which measurement is missing; do not reuse Collaboration's old counts/timings as TestLab results.

## Receipt

Store sanitized graph/watch output, timing samples and screenshots in the task's ignored artifacts location. Permanent docs summarize exact conditions, representative findings and limits; do not commit bulky binaries, secrets or temporary handoff paths. A useful successful result is a proven light real-renderer sandbox with an honestly measured loop, not an unsupported percentage target.
