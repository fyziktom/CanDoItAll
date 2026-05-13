# Scope Inventory

## In Scope For Implementation Bundle

- Plugin abstractions in `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions`
- Plugin catalog, persistence, API, and UI in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins` and `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\PluginsApi.cs`
- Plugin secret broker integration in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\PluginSecretBroker.cs`
- Workflow executor catalog, invoker, descriptor source, payload policy, and workflow validation surfaces.
- Workspace command execution, environment policy, process host, receipt writing, and command plan builder.
- Tests under `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit` and `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- Browser-visible plugin settings page and workflow editor availability diagnostics.

## Out Of Scope For This Bundle

- Arbitrary third-party plugin marketplace installation.
- OS/container sandboxing for plugin execution.
- Inline arbitrary PowerShell from plugin configuration.
- Full Docker UI beyond the sample workflow/plugin proof.
- Rewriting the entire workflow engine.
- Replacing the existing plugin catalog module when a focused refactor can harden it.

## Source Ownership Notes

- Plugin abstractions should stay generic and must not depend on modules, EF, Blazor, or Docker implementation details.
- Plugin module owns catalog, installation state, grant state, connection settings, and settings UI.
- AgentFramework owns workflow executor invocation and generic host-command execution policy.
- Security module owns secret storage and runtime resolution, with a canonical plugin-facing adapter.
- Web API should remain thin and delegate business rules to application services.
