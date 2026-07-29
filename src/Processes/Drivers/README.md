# Process Drivers

Process drivers connect generic process steps to execution strategies.

| Project | Responsibility |
|---|---|
| [Abstractions](CanDoItAll.Processes.Drivers.Abstractions/README.md) | Driver catalogs, packages, strategies, tokens, and execution adapter contracts |
| [Standard](CanDoItAll.Processes.Drivers.Standard/README.md) | Built-in adapter descriptors and strategy factories |

Drivers translate execution behavior; process state transitions remain owned by the
process runtime.
