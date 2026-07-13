# Architecture Follow-Up Request

User follow-up:

- Completion evidences, runtime process dispatching, and prompt fragment compositions should be in drivers.
- The architecture must assure correct isolation through interfaces and drivers so the runtime can be improved or switched for maintainability.
- Do not create bad dependencies from Processes to the MAF wrapper.
- The MAF wrapper must be below Processes in the dependency tree.
- Improve the bundle accordingly.

Normalization:

- Processes projects own contracts, immutable runtime state, scheduling/claim orchestration, and driver ports.
- Driver implementations own step execution dispatch behavior, prompt fragments, completion-evidence validation, provider/tool/runtime policies, and driver-specific recovery behavior.
- AgentFramework/MAF process support must implement driver ports from below the Processes boundary.
