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

**Decision:** Explicitly selected strong providers fail closed. `Auto` selects only the platform baseline defined by ADR-C08; it never changes providers after selection.

**Reason:** Capability truthfulness and confidentiality.

**Rejected:** Silent InMemory/plaintext/insecure file fallback.

## ADR-C08 — Local startup has an explicit platform protection baseline

**Decision:** Windows `Auto` selects current-user DPAPI and reports `Strong`. Unix `Auto` selects `LocalUserFile`, which preserves the AES-256-GCM local file format, enforces `0700` directories and `0600` files, reports `BasicLocal`, and warns that code running as the same operating-system account can read its colocated key. `DataProtectionFile` remains a Development/migration-only provider name. Operators select Keychain, Secret Service, or an external wrapping key when the same-user threat is in scope.

**Reason:** A local installation must start without requiring an interactive keyring or externally managed vault, while capability reporting must distinguish encrypted-at-rest local storage from an operating-system or externally protected vault.

**Rejected:** Blocking all Unix startup without a stronger vault; silently downgrading an explicitly selected strong provider; describing the key-beside-ciphertext tier as strong or same-user isolated; using the basic tier on Windows where DPAPI is built in.

## ADR-C09 — Native Keychain execution is a post-bundle validation obligation

**Decision:** Keep the real macOS Keychain adapter and its native-client contracts, but defer genuine macOS execution to `MACOS-KEYCHAIN-VALIDATION-001`. The missing actual-host run does not block C2, C4, or later bundle execution, and the provider remains explicitly actual-host unverified until the follow-up passes.

**Reason:** The operator does not currently have an authorized macOS execution environment and explicitly chose to continue implementation while preserving the validation debt and bounded support claim.

**Rejected:** Claiming the Keychain profile was actually tested; deleting the adapter or its contracts; weakening explicit-provider fail-closed behavior; continuing to block unrelated Windows/Linux/platform work on unavailable hardware.

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
