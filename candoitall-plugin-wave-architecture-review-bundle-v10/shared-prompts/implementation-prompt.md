Use this bundle to close the remaining plugin-wave architecture blocker after the claimed phase9 completion.

Required workflow:
1. Read this bundle and the repeat-offender notes from v7/v8/v9.
2. Fix only what is still open for phase10 closure.
3. Do not claim closure until:
   - all required phase10 test names exist,
   - the phase10 gate passes,
   - runtime validation output is attached.

Implementation priorities:
- remove every direct/transitive persistence mutation from `ProjectStructureAssemblyService.LoadAsync(...)`,
- move stale projection cleanup to an explicit maintenance/repair boundary,
- add the exact required tests from `plan/02-closure-evidence-checklist.md`,
- add unknown provider/resource manifest proof across all shared field types.

Important anti-evasion rules:
- moving cleanup to another method on the read path is not closure,
- renaming the cleanup is not closure,
- built-in-only plugin tests are not enough for the next plugin wave.
