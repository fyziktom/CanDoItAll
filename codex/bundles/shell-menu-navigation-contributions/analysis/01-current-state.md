# Current State

- Standard sidebar navigation items in `AppShell.razor` still use `TooltipTarget` without a delay, while overflow-card and opened-work-card tooltips already use a two-second delay.
- `MainLayout.razor` still wraps the bottom `Settings` menu action in a tooltip without delay. `Switch Database` is already a popup trigger without a tooltip wrapper.
- `ShellNavigation.Items` is a static Web composition list. It includes `Agents` followed directly by `Resources`.
- The AgentFramework module already owns a routable Workflows page at `/agents/workflows`, and `AgentsHomePage` links to it from the Agents page.
- There is no module-level navigation contribution contract today, so adding `Workflows` directly to the static Web list would solve the visible symptom but not the generic module requirement.
