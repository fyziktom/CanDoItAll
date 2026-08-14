# Core key decisions

1. Persist logical paths with `/`; preserve physical paths as native host data.
2. Translate legacy backslashes only in known logical fields.
3. Use root/volume filesystem semantics; do not equate OS identity with case behavior.
4. Establish atomicity, cross-process coordination, links, and Unix modes before migrating storage or secrets.
5. Mark absolute physical records host-bound and require rebind on a foreign host.
6. Auto secret selection fails fast rather than selecting unsupported or insecure persistence.
7. Key-ring protection bootstrap cannot depend on the protected ring.
8. Headless operation uses an explicit non-interactive secure provider.
9. Narrow purpose-owned adapters replace both branch sprawl and a giant platform service.
10. Core C4 is required before any runtime/tools/process implementation.
