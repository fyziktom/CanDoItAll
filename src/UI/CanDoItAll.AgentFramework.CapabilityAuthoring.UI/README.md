# Capability definition authoring

`CapabilityAuthoringForm` renders the three-step creation wizard and Identity,
Configuration and Raw detail tabs. Both use the same identity, MCP, Skill, Tool,
setup-result and raw-field components. This is definition authoring; agent assignment
and published verification remain native application operations.

The form acquires one draft/edit context per target and owner lifetime. Its operation
record receives immutable submissions and explicit accepted identities. Raw invalid
numbers and JSON survive tab changes. Setup runs only on explicit activation, and a
diagnostic from an earlier draft revision cannot verify the current draft. Uploaded
skill text is limited to 1 MiB and never executed by this library.

The module's `NativeCapabilityAuthoringHost` supplies catalog and setup delegates,
including the native environment-name comparer and profile cancellation. Runtime
compilation, paths, credentials, HTTP/process/MCP effects and atomic storage remain
outside this project. The independent `CapabilityAuthoring.UiSandbox` supplies stored
fixtures; its simulated setup counters are not native execution evidence.

Run the sandbox and its independent tests using the commands in
[Testing](../../../docs/testing.md#capability-authoring-ca1).
