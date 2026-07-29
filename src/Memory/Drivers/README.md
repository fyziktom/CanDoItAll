# Memory Drivers

Drivers isolate separately owned Memory implementations from the provider-neutral
application model.

| Project | Responsibility |
|---|---|
| [Cognitive Memory](CanDoItAll.Memory.Drivers.CognitiveMemory/README.md) | External Cognitive Memory service adapter |

Add a driver only when a provider requires behavior that does not fit the generic HTTP or
MCP transports.
