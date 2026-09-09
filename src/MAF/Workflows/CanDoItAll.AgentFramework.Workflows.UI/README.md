# Workflows UI

This .NET 10 Razor class library contains controlled, service-free rendering for
the Agent Workflows shell, catalog, history, templates, overview and analytics.
Its inputs are immutable presentation records; user actions emit typed intents.

Dependencies are ASP.NET Components.Web, CanDoItAll.Components.BaseLib and
CanDoItAll.Components.Charts. Repository builds resolve the Components packages
to the authoritative sibling source. The library does not reference workflow
runtime, persistence, Module, provider, project or workbench implementations.

WorkflowsPage in Modules.AgentFramework owns routes, accepted targets, service
calls, navigation and effects. Its original WorkflowCanvasEditor and owned
dialogs are composed through named slots. Overview and analytics query hosts
map accepted results into these Surfaces. The shared workflow stylesheet is a
static web asset included by WorkflowShellSurface; overview-specific styles
remain scoped to their renderer.

The existing AgentFramework.UiSandbox renders twelve deterministic Workflows
scenarios in Parity and Fast modes without production workflow services.
WorkflowSurfaceTests and WorkflowOwnershipTests in the Components test project
cover presentation intents and the real host's target lifetimes.

Build this project with `dotnet build` from its directory. Repository validation
and focused test commands are documented in [Testing](../../../../docs/testing.md).
