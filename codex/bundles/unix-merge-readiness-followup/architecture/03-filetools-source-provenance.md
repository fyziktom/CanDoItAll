# FileTools source provenance

## Problem

The reviewed direct-source mode depended on FileTools commit `f31e20d054003348c7557b9634e0838fc5996ae0` plus uncommitted changes. `UseLocalCanDoItAllLibraries` is currently inferred from sibling directory presence, and `CANDOITALL_FILETOOLS_DIRECT_SOURCE` is then treated as implementation validation.

## Required target

1. Package mode is the default and authoritative mode.
2. Local source mode is enabled only through an explicit developer/operator property.
3. FileTools exposes a versioned desktop-launch contract marker from committed source.
4. CanDoItAll validates the expected marker/source anchor before reporting desktop implementation as verified.
5. Missing/mismatched marker either fails the explicit source build or marks desktop capability unverified/unavailable; it must not silently claim validation.
6. The final handoff records exact clean SHAs for CanDoItAll, Components, and FileTools.

For alpha, package mode may keep desktop launching disabled. This is preferable to a false capability claim.
