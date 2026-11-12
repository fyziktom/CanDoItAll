# Remove durability work from transient case detection

The retained third SQL cohort confirms both catalog setup repairs pass. It still fails
the unchanged responsiveness criterion. Nested activities show workspace coordination
waits of 214–654 ms and cancellation commits around 353 ms; this is not a five-second
wait on the SQL holder. Data-file writes include repeated path-policy creation.

`PhysicalFileSystemPathPolicy.DetectCaseSensitivity` creates an empty unique probe,
opens it with `WriteThrough`, flushes to physical media, tests an alternate-case name
while the handle remains open, then deletes it. DurableFileWriter recreates these
policies at several security boundaries for every real publication. The transient
probe has no crash-recovery or persistence contract. Synchronous durability here is
unnecessary work on every path check, including reads and lock acquisition.

Remove only the probe's WriteThrough option and physical Flush. Keep CreateNew,
the open handle during alternate-name observation, permissions/sharing, unique identity,
exception behavior, cleanup and every containment/reparse revalidation unchanged.
Do not cache case sensitivity or assume Windows/macOS is case insensitive. Actual
data/journal writers keep their WriteThrough, Flush and atomic replacement protocol.
No project reference, public contract, lock order or module boundary changes.

Validate the existing physical-path and durable-writer tests, catalog dependency races,
selected authority and publication recovery/validation/budget cases, plus the same
small-file SQL workload with its original one-second threshold. Keep the real seeded
catalog size and retained activity diagnostics. Existing case-detection/cleanup and
symlink tests provide behavioral proof; do not write a test which merely checks the
absence of a Flush call. A passed Windows run is not Linux/macOS execution evidence.
Run portability-static and keep the result independent of performance acceptance.
