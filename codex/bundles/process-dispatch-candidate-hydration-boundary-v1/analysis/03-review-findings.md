# Review Findings

## Completed Work From Prior Bundle

The claim/route boundary work is complete enough to proceed. It established route facts, execution-run selection, guard lease, start-transition planning, route decisions, and finalizer context factory.

## Remaining Issues

1. `LoadDispatchCandidateHeadersAsync` still mixes run lookup, eligibility, lease expiry, status filtering, ordering, and header shaping.
2. `LoadDispatchCandidateAsync` still performs too much:
   - run and definition loading,
   - dispatchable claimed step filtering,
   - work brief lookup,
   - all step run loading,
   - artifact record loading and external-reference key extraction,
   - step definition and role requirement lookup,
   - run assignment lookup,
   - artifact input and branch outcome loading,
   - conditional dependency shaping,
   - expected artifact loading,
   - artifact-input prompt preparation,
   - subprocess/workflow/direct-agent candidate creation,
   - execution-run recovery selection,
   - manual recovery directive loading,
   - technical-agent binding and project-structure read-access mutation.
3. Technical-agent binding is a natural future driver-related seam, but it should stay module-local now.
4. Candidate/evidence intent vocabulary can be documented now, but production driver API must wait.
