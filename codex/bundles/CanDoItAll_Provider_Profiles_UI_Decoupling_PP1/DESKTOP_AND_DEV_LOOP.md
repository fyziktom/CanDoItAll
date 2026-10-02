# Large desktop and measured development loop

**Primary viewport: 1920 × 1080 at 100% zoom.** Optional second large viewport: 1600 × 1000 only when it proves a specific functional problem. No new phone/tablet/small/medium tuning sweep. Existing unrelated responsive tests are not deleted or weakened, but do not expand this assignment into responsive redesign.

Validate real focus/keyboard behavior, native Enter/input timing, scroll ownership, entire action/footer visibility, nested dialog layering, tables and style delivery on the supported desktop. A small confirmation dialog inside a large desktop is still legitimate. Do not replace actual widgets to simplify screenshots or impose arbitrary KPI cards at the expense of the editable surface.

Before moving code, capture the warmed original provider host: SDK/configuration, source triple, evaluated graph, watch set, initial build/startup versus hydration, and three ordered edit-to-visible samples for Razor/C# and actually owned scoped CSS/JS. Then measure the same workload in the final product and independent provider sandbox without concurrent tests/builds. If no feature JS exists, report not applicable rather than creating it.

Record first/warm observations, target readiness, method and limitations. C# may require an explicit normal rerender rather than recreation of existing state; identify it. Restore probe bytes and verify their hashes. Never use a failed restoration as final source.

Measure sandbox graph/watch reduction, not a promised constant project count. Preserve A2, Projects P1/Files, Workspace and RecordBrowsing closures. Whole-Web latency can remain similar or vary; no uniform speedup claim follows just from a smaller leaf. The goal is a complete realistic independent loop for later UI tuning.

Assets: use existing Tailwind/base theme, fonts/icons and required component registrations. Keep production CSS as content where needed, not a Web project reference. Check independent publish, not just `dotnet run` inside the source tree.
