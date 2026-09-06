# Proven unconfirmed-write recovery lessons

Evidence: [Providers-02D](../../UI_Providers_02D_Recovery_Bundle/execution.md), canonical database, component and browser proof.

1. Unconfirmed non-idempotent writes need immutable attempt identity before persistence. A proposed entity identity is not a committed editor identity.
2. Canonical verification is distinct from catalog refresh and replay. Failed reads never prove absence.
3. After authoritative absence, controlled retry retains identity and submission. A racing create conflict returns to verification rather than discarding duplicate protection.
4. Recovery may need to outlive presentation. Scope it by target/attempt while retaining cancellation/generation fencing for UI publication.
5. Verified authoritative tokens/action state must preserve later editable values, EditContext and the user's current section.
6. Changes discovered by verification need at-most-once semantic delivery per attempt. Closing a component suppresses callbacks without asserting rollback.
7. Unknown-write API contracts identify verification and prohibit automatic replay. Permanent audit/public identities need reference-specific remediation.
8. Intervening revisions can make historical causation unknowable. Document scope/durability limits instead of implying a durable journal.

These rules describe responsibilities, not mandatory provider-specific types.
