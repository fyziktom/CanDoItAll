# Core architecture decision records

## ADR-C01 — Separate logical and physical paths

**Decision:** Canonical logical paths serialize with `/`; physical host paths remain native and host-bound.

**Reason:** A global separator policy is unsafe on Unix and cannot represent portable versus local state.

**Rejected:** Repository-wide backslash replacement; using `Path.DirectorySeparatorChar` in persisted locators.

## ADR-C02 — Filesystem semantics are root-specific

**Decision:** Logical identifiers are ordinal. Physical path equality/case behavior is determined per trusted root/volume, with explicit uncertainty handling.

**Reason:** macOS volume case behavior is not determined solely by OS.

**Rejected:** `OperatingSystem.IsWindows() ? OrdinalIgnoreCase : Ordinal`.

## ADR-C03 — Persisted physical paths are host-bound

**Decision:** Absolute roots/executables have format/platform/host state and require rebind on a foreign host.

**Reason:** Cross-OS import must not reinterpret or execute foreign paths.

**Rejected:** `Path.GetFullPath` on every stored string.

## ADR-C04 — Secure providers fail closed

**Decision:** `Auto` selects only a proven operational provider. Production does not fall back to the current file-vault key-beside-ciphertext design.

**Reason:** Capability truthfulness and confidentiality.

**Rejected:** Silent InMemory/plaintext/insecure file fallback.

## ADR-C05 — Key bootstrap is acyclic

**Decision:** Data Protection key-ring protection is configured before secrets that depend on that ring.

**Reason:** Existing secret/control-plane ciphertext must remain decryptable.

**Rejected:** Storing the only ring-decrypting material in the ring itself.

## ADR-C06 — Narrow owner-specific adapters

**Decision:** Platform variation is isolated by purpose and ownership. No broad platform service is introduced.

**Reason:** Preserve current MAF/Processes/security/storage dependency boundaries.

**Rejected:** One service with OS name, paths, terminals, secrets, process, and feature booleans.

## ADR-C07 — Headless core stabilizes before runtime integrations

**Decision:** Core Gate C4 precedes runtime/tools/process work.

**Reason:** Data migration and runtime ownership have different risk and evidence requirements.

**Rejected:** One long implementation stream through paths, keys, Workbench, MCP, and Processes.
