# Component composition rules

1. Prefer several focused components over one universal conversation component.
2. Use typed presentation records for data and small callbacks for effects.
3. Use named render fragments for truly optional source-owned features.
4. Do not add a `Kind` switch that branches between Agent and Simple Chat.
5. Do not add dozens of `ShowX` booleans. A small number of visual options is acceptable; product capabilities belong in composition.
6. Do not inject backend services into neutral Razor components.
7. Do not pass `IServiceProvider`, factories, repositories, coordinators, or DbContexts through component parameters.
8. Do not interpret opaque ids or metadata inside neutral components.
9. Keep source-specific text and semantics in adapters when they are not genuinely common.
10. Keep current component entry points until all consumers migrate.
11. New neutral components must have direct bUnit tests that do not construct the old Agent UI runtime.
12. Existing Agent facades must have adapter/compatibility tests proving current public behavior.
13. Use CanDoItAll BaseLib composition and semantic tokens before custom structural markup or CSS.
14. Preserve a single, known scroll owner in each window/dialog/panel.
15. Large-screen desktop is the application validation target; preserve but do not redesign smaller layouts.
