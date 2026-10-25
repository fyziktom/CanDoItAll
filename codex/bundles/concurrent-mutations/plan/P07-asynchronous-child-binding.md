# Asynchronous original child binding

Status: designed before implementation after signed claim-lifetime checkpoint d4c4239f2;
implemented with [192 passing selected cases](../validation/2026-10-19-asynchronous-child-binding.md).

The concrete Workbench target requires current Agent/Project checks and a finite write
context from the database. The existing synchronous original-binding delegate cannot
perform those reads without blocking or moving them ahead of the exact-child lookup.

Make the binding delegate asynchronous within the existing new-only admission section.
Give it the original candidate and admitted source session as one typed context, plus
cancellation. The actual candidate timestamp and metadata then remain available for
canonical Process claim verification. Retained reads and retained initial runs must never
call this delegate or acquire a replacement write context.

After the awaited callback, validate the complete inherited journal and recheck current
source dispatch authority against that original session before returning it for initial
save. A claim, profile, fingerprint or permission change during database reads must not
admit a new execution. Cancellation must prevent save and release the child lease. Do
not add sync-over-async, a second admission store or a fresh binding on retry.

Keep parent validation and the registered participant authoritative. The callback is an
in-process owner seam, not an HTTP request or permission grant. Concrete Process source
selection, Workbench interpretation, publication and manager integration remain required.

Prove asynchronous suspension before initial save, cancellation/failure cleanup, source
revocation during the callback, and exact retry reuse. Repeat the shared/inherited
workspace, journal, MAF and architecture selection after serialized Release builds and
the complete portability gate. Retain source/runtime identity and observed resources.
