# Selected Agent authority snapshot integration

This continues the existing narrow file publication protocol and the runtime-policy
design. The supported first scope is an original interactive Agent/run/chat in its
canonical organization workspace. It supplies current authorization evidence for a
later planning publisher; it does not implement or activate RunSnapshot/StepSnapshot.

The former coarse catalog guard holds every organization writer during SQL publication.
The selected snapshot must hold only the complete existing Agent/run/chat key set while
releasing the workspace guard before returning. It captures exact original run/journal
revisions, immutable saved run/chat values and the current complete selected Agent and
capability configuration. A catalog revision bump from unrelated Agent B cannot refresh
or invalidate A's saved planning input. Changes to A, its run, shared capabilities or
other catalog-wide authority continue to cooperate with the existing writer protocol.

Core owns the typed expectation and lease port. Persistence owns acquisition, coherent
file observation, defensive copying and terminal key release. The existing file-store
catalog facade delegates lifetime to an independent lease class. No new partial file,
project reference, SQL dependency, provider registration or generic state machine is
needed. Unsupported stores explicitly refuse instead of acquiring a coarse fallback.
The expectation is untrusted original identity, never a grant of permission or provider
provenance. A later Core planning owner must validate the saved decision generation,
basis, live origin and original policy before using this authority for publication.

Reuse the reviewed eight-file source overlay, merging its two changed predecessors.
Current catalog contracts and persistence now also support execution-admission recovery
under canonical organization and separate Project/Sandbox publications. Preserve those
members; do not replace whole older files. Three new Unit cases and six new file-owner
Integration cases are authored in the overlay. The canonical Agent module adapter
forwards the explicit selected port after validating its existing host binding; it does
not wrap a planning lease as deletion-recovery authority. Build Core, Persistence and
the changed Agent module directly,
refresh owning test outputs, then discover the new cases plus existing catalog-lease,
prepared-value and publication validation/recovery regressions. Use actual independent
store instances and key-wait barriers for unrelated progress, revocation, exact identity,
partial-acquisition cancellation, shared pending evidence and terminal disposal.

The new lease caches its terminal release task. Concurrent/repeated disposal observes
the same completion or failure; a second caller cannot report release while the first
is still releasing keys. This follows the newer canonical recovery lease behavior.

CodeAnalytics baseline `snap-20261019015722-85d8bb99` covers Models, Core and Persistence:
406 documents, 1,726 types and 3,494 dependencies. Its existing four cycles are the
comparison baseline; there are no non-advisory workspace errors. Recheck dependencies,
direct builds and portability after integration. Counts are confirmed by built discovery,
not inferred from test attributes.

Whole-catalog normalization before selected acquisition and recursive run evidence
hashing remain measurable costs. This slice must not claim the coarse SQL bottleneck is
qualified until the actual publisher cost comparison runs. Homogeneous cooperating
writers remain required. Schema-six immutable decision admission, saved factual input,
Refresh/Cancel/Replan, active-work accounting and Process/Workflow propagation follow
as separate runtime responsibilities. Persisted settings alone do not close them.
