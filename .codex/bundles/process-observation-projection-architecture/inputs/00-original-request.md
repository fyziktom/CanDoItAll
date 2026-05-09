# Original Request

Date: 2026-05-08

The user asked for architecture planning only, in the form of a CanDoItAll bundle with subbundles. No production implementation should happen during this turn.

Requested focus:

- Analyze how to improve communication between process core and UI.
- Use `analyzing-dotnet-performance`.
- Use current Microsoft Learn best practices for Blazor through the Microsoft Learn MCP.
- Prepare for a more flexible live Processes UI that can show what is happening across different processes and stages, open details in dialogs, and stay usable when many processes are running.
- Prepare proper observation services first, because direct live UI refresh can overload process core.
- Consider `IMemoryCache`, including update frequency, overload risks, and avoiding a split source of truth.
- Treat the future UI as mostly observational, not a mutation surface.
- Plan for an advanced future where an AI conversation can change the process dashboard focus, open suitable details, and offer deeper drill-down buttons.
- Map the existing Processes page and current lazy-loading/performance improvements.
- Preserve all current functionality.
- Keep process logic generic. Specific instructions belong in process step definitions, agent tools, or skills.
