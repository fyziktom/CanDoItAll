# Requirement Traceability

| Raw note | Exact wording or artifact | Requirement IDs | Owning subbundle | Planned proof |
| --- | --- | --- | --- | --- |
| N001 | "Analyze how is it possible to use ApexCharts in C#." | R001, R002 | `01-01-wrapper-foundation` | Current-state analysis plus wrapper implementation. |
| N002 | Working examples in `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs`, especially `ConsumptionBarGraph.razor` and `EnergyPricesGraph.razor`. | R001, R007 | `01-01-wrapper-foundation`, `02-02-sandbox-chart-examples` | Source references, chart type support, area/fill examples. |
| N003 | Existing `ApexGraphComponentBase`. | R001, R004 | `01-01-wrapper-foundation` | Shared defaults mirrored in wrapper options without copying app-specific dependencies. |
| N004 | "We are using Blazor-ApexCharts package" and cloned package source. | R002, R005, R006 | `01-01-wrapper-foundation` | Package reference, service registration, asset component, build. |
| N005 | "Create our wrapper as new CanDoItAll.Components.Charts." | R003, R004 | `01-01-wrapper-foundation` | New project, solution entry, public API review, build. |
| N006 | "Wrapper over external library for case we would decide to use different library in the future or create own." | R004, R005, R006 | `01-01-wrapper-foundation` | No direct Apex component usage in sandbox consumer page; CanDoItAll-owned models/enums. |
| N007 | "Use our components sandbox to create some examples. You must add it as new page in sandbox." | R008 | `02-02-sandbox-chart-examples` | New `/groups/charts` route in catalog and browser proof. |
| N008 | "Add few common cases like pie chart, line chart with one or multiple lines, tuning of colors, filing color underline, adding additional labels, etc." | R007 | `02-02-sandbox-chart-examples` | Browser-visible examples and screenshot review. |
| N009 | EnergoApp screenshots. | R007, R008, R009 | `02-02-sandbox-chart-examples`, `03-03-validation-and-closure-proof` | Visual review against dense line/area, legend, toolbar, labels, summary context. |
| N010 | "Use candoitall-bundle-workflow." | R010 | All subbundles | Bundle validators, subbundle gate rows, browser analytics, raw-note closure. |
