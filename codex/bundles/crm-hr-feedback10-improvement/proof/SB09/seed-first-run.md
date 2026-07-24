# SB09 API-Only Seed First Run

Run date: `2026-07-24`

## Command

```powershell
& 'C:\repositories\CanDoItAll\output\runtime\crmhr-feedback10-final\seed-crmhr-demo.ps1' -ApiBase 'http://127.0.0.1:5032/api/crm-hr'
```

- Exit code: `0`
- Operator script: `repo://output/runtime/crmhr-feedback10-final/seed-crmhr-demo.ps1`
- Script bytes: `37196`
- Script SHA-256: `23F91D36E2C2BB972C9308C3176C1CA3B6F369AB9CC955AB5D346E93D2F96BB9`

## Identity And Transport Contract

- The operator queried and mutated only the public CRM-HR HTTP family rooted at `/api/crm-hr`.
- Stable business identities use the `DEMO-CRMHR-*` external-code namespace and scenario marker `[CRMHR-DEMO-2026]`.
- Representative identities include `DEMO-CRMHR-ORG-AURORA`, delivery-unit codes, workforce codes such as `DEMO-CRMHR-PERSON-AMINA` and `DEMO-CRMHR-PERSON-LUCAS`, and candidate codes such as `DEMO-CRMHR-CANDIDATE-OMAR`.
- The operator contains no SQL, EF access, database connection, application-startup registration, cleanup endpoint, or product fixture branch.

## Observed Scenario

The completed run produced a heterogeneous persistent scenario that the public API read back as:

- `29` demo parties inside `78` total parties;
- `32` workforce records;
- `12` skill-catalog entries and `40` party-skill assignments;
- `5` capacity blocks;
- `8` recruiting applications and `9` interviews;
- `4` lifecycle tasks and `3` candidates with support assignments;
- one completed recruiting-to-workforce conversion;
- application stages `Applied 1`, `Screening 1`, `Interviewing 2`, `Offer 1`, `Hired 1`, `Rejected 1`, and `Withdrawn 1`.

The data includes organizations and delivery units, active employees, a contractor, capacity variations, multiple recruiting outcomes, scheduled/completed interviews, manager/buddy/mentor assignments, onboarding tasks, and a completed hire. It does not invent invoice, purchase, or bought-financial facts.

## Behavioral Conclusion

- Semantic positive: the API-only client created or reconciled a linked party, workforce, skill, capacity, recruiting, interview, support, lifecycle, and conversion scenario that remained visible through normal API reads and the product UI.
- Shallow-pass trap rejected: this is not a homogeneous employee list, test-only endpoint, startup fixture, or direct database insert.
- Anti-stub audit: the operator performs real search-before-create and canonical command calls; it does not return acknowledgements without readback.

`Pass`.
