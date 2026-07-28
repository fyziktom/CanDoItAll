# Cognitive Memory Remote Driver

This experimental project is the client-side adapter from the generic CanDoItAll
Memory runtime to the standalone Cognitive Memory service. That service is work in
progress and is not published yet.

The boundary is intentionally process-based:

- this project references only the generic Memory abstractions, application layer, and HTTP driver;
- it does not reference source or packages from `CanDoItAll.CognitiveMemory`;
- compatibility is the versioned Memory Protocol v1 JSON wire contract, including the native-remote profile keys owned by `CanDoItAll.Memory.Abstractions`;
- service credentials are resolved from an environment-variable name stored in the provider profile; raw credentials are rejected.

Registration is opt-in through `AddNativeRemoteMemoryProviderDriver`. The stable `MemoryProviderDriverKind.NativeRemote` value remains in the generic protocol model for persisted-profile compatibility, while the provider-specific configuration and implementation stay in this assembly.

The current adapter supports context queries and health checks. Configure only those capabilities in its provider manifest. Additional protocol operations must be added here behind the corresponding generic driver interfaces rather than leaking Cognitive Memory implementation types into the host.
