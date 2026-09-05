# Owner review and execution decisions

Input: the owner's review of d3ba280a431bfe74ce03a72638ac06dff47de660 in this task. The quoted review is evidence and design advice; the owner's surrounding request authorizes repair, bundle preparation, implementation and tests.

Accepted: fix initial catalog/reload overlap and session-owned nested agent dialogs in SB09; next perform provider typed selection/sections, read seam, cancellation and fail-closed core reads. Preserve secret references on explicit metadata partial failure.

Design refinement: use one ProviderProfilesSession per rendered panel, owning ProviderProfilesState and its replaceable target lifetime. A separate page state plus independent editor selection is unnecessary in an unrouted panel. The session's draft Id is a persistence value, never the authority used by the tree. No DI registration for mutable session state.

Preserve New during pending initial reads. Initial automatic first selection may run only before any explicit selection/New action. A failed read retains its target and Retry never silently substitutes another provider. Explicit Refresh on an existing target keeps the existing successful reload behavior; draft-preserving mutation reconciliation remains PROVIDERS-02.

No mechanical reuse of AgentEditorSaveOutcome: provider registry commit analysis belongs to the next commands child.
