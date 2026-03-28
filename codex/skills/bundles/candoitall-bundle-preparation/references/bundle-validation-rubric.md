# Bundle Validation Rubric

## QA Review

Confirm that:

- the raw request or artifact is preserved
- normalized requirements are explicit
- every requirement has at least one bundle destination
- every raw input is mapped to an owning subbundle or an explicit exception
- each subbundle has observable acceptance criteria
- each dependent subbundle has a clear prerequisite and progression gate
- UI-heavy work defines screenshot or browser validation questions
- proof expectations are specific enough to fail

## Senior C# Blazor Architect Review

Confirm that:

- the bundle names the real source files or projects
- the phases avoid unnecessary big-bang changes
- shared-library ownership and layering stay clear
- critical foundation subbundles are labeled and validated more deeply
- test and validation scope is realistic for the repo
- assumptions and risks are surfaced instead of hidden

## Senior Manager Review

Confirm that:

- the critical path is obvious
- dependencies between subbundles are explicit
- the mermaid dependency map is usable by another human reviewer
- the bundle is detailed enough to hand off safely
- the sequencing avoids wasted rework
- completion evidence is defined

## Rejection Conditions

Do not accept the bundle as implementation-ready when:

- there is no traceability
- a subbundle has no proof rules
- a subbundle has no prerequisite or progression gate even though later work depends on it
- a subbundle is too broad to complete in one coherent pass
- the bundle relies on unstated repo assumptions
- UI validation is reduced to “looks fine”
