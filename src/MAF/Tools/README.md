# Agent Tools

| Project | Responsibility |
|---|---|
| [Tooling](CanDoItAll.AgentFramework.Tooling/README.md) | Runtime-tool provider contracts and metadata |
| [Abstractions](CanDoItAll.AgentFramework.Tools.Abstractions/README.md) | Agent tool descriptors and contracts |
| [Tools](CanDoItAll.AgentFramework.Tools/README.md) | Tool descriptors, setup, diagnostics, and invocation support |
| [Documents](CanDoItAll.Tools.Documents/README.md) | Document and spreadsheet tool implementations |

First-party product tools are registered by their owning module through the provider
contract. Tool attachment remains subject to capability, access, approval, and workspace
policy.
