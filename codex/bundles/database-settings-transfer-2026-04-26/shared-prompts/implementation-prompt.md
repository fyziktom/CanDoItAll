# Implementation Prompt

Implement the bundle phase by phase. Keep transfer orchestration generic in Infrastructure, module copy logic in each owning module, and UI code limited to rendering descriptors/previews and invoking the service. Do not expose token or API key cleartext. Use explicit source/target database profile contexts for every transfer operation.
