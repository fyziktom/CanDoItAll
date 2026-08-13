# Architecture and safety invariants

1. **Persistence is versioned, not guessed.** A hash version is established by
   explicit metadata or deterministic payload shape, never by creation time.
2. **Legacy data is read-only evidence.** V1 plans may be inspected and
   recompiled, but never executed as though they had sealed V2 host
   capabilities.
3. **Start returns ownership or nothing.** A process session is not observable
   until the OS ownership boundary and executable identity are established.
4. **No PID-only recovery.** Missing process boundary, start identity,
   executable fingerprint, owner evidence, or command evidence prevents
   termination.
5. **Container profile is truthful.** Headless container capabilities are
   reported from dependencies actually present in the image.
6. **MAF is an adapter.** MAF package behavior does not become the source of
   CanDoItAll authority, workspace, approval, or persistence semantics.
7. **Package mode is canonical.** Clean builds cannot silently depend on
   sibling working trees.
8. **Deferred is not passed.** macOS and enterprise-vault work remains
   explicitly unverified without blocking this merge-to-development boundary.
