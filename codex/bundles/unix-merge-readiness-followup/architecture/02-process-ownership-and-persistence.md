# Process ownership and persistence correction

## Plan hash versioning

Introduce an explicit persisted hash algorithm/version contract independent of the existing template/process schema version.

Recommended shape:

- `ProcessPlanHashAlgorithmVersion.LegacyV1`
- `ProcessPlanHashAlgorithmVersion.HostCapabilitiesV2`
- current compiler emits V2;
- missing serialized version is interpreted as V1 only for records created before the V2 migration boundary;
- mapper verifies with the declared algorithm before any transformation;
- migration writes the upgraded payload/hash in one transaction and retains rollback evidence;
- tampered records fail before migration.

Do not simply set a default V2 value on deserialization. That would make old payloads unverifiable.

## Legacy capability disposition

After V1 verification, derive V2 host requirements only from authoritative immutable inputs. If derivation is ambiguous, persist a typed non-executable state and require explicit plan recompilation. Do not populate `[]` and continue execution.

## Process trees

The owner must create an OS-level ownership boundary at start time:

- Windows: Job Object with kill-on-close or an equivalent durable owned-tree mechanism.
- Linux/macOS: dedicated process group/session and group signal/kill, or an equivalently proven implementation.

A snapshot-only descendant walk is insufficient as the sole guarantee because the root may exit and children may reparent. Preserve exact root identity and add owned-group identity to diagnostics/evidence.
