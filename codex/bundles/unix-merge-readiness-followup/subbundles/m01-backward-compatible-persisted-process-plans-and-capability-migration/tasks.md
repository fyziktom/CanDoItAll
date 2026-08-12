# Tasks

- [ ] Create an exact pre-host-capability persisted-plan fixture from the parent implementation, including original JSON and hash.
- [ ] Introduce an explicit plan-hash algorithm version independent of template/process schema version.
- [ ] Implement legacy V1 and current V2 canonical hash verification; missing version is legacy only within a bounded migration rule.
- [ ] After V1 verification, derive V2 capability/profile requirements from authoritative immutable data or mark the plan typed `NeedsRecompile`/non-executable.
- [ ] Implement transactional/idempotent migration, restart, tamper rejection, interruption recovery, and rollback evidence.
- [ ] Ensure database column defaults or deserialization defaults cannot mean “no host requirements” for an unknown legacy plan.
- [ ] Preserve exact plan immutability and current tamper detection.
