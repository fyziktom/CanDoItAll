## Assumptions and risks
- This review is based on deep static analysis of the uploaded repository snapshot.
- The container used for this review does not provide `dotnet`, so no trustworthy build/test/run evidence could be produced here.
- Therefore, the bundle is an execution-grade refactor input plus a stronger gate package, not a claim that runtime behavior has been validated.
- Some findings are closure blockers for the plugin wave even if current user-visible behavior seems acceptable. This is intentional: the goal is not only “works today,” but “safe base for the next wave.”
