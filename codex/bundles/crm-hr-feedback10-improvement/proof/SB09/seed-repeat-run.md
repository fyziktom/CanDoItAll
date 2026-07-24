# SB09 Seed Repeat And Identity Reconciliation

Run date: `2026-07-24`

## Command

```powershell
& 'C:\repositories\CanDoItAll\output\runtime\crmhr-feedback10-final\seed-crmhr-demo.ps1' -ApiBase 'http://127.0.0.1:5032/api/crm-hr'
```

- Exit code: `0`
- This was an immediate reconciliation run against the already-seeded Release host.

## Observed Operation Counters

| Entity or operation | Created/written/replaced/converted | Reused |
| --- | ---: | ---: |
| Parties | `0` | `29` |
| Relationship sets | `0` | `20` |
| Workforce profiles | `0` | `20` |
| Skills | `0` | `12` |
| Party skills | `0` | `40` |
| Capacity blocks | `0` | `5` |
| Recruiting applications | `0` | `8` |
| Interviews | `0` | `9` |
| Lifecycle tasks | `0` | `4` |
| Support assignments | `0` | `3` |
| Recruiting-to-workforce conversions | `0` | `1` |

The reconciliation reported zero creates, writes, replacements, or new conversions across every tracked operation. It reused the stable external-code/business identities and marker-owned child records.

## Adversarial Conclusion

- The repeat check proves identity idempotency directly from operation counters, not by comparing aggregate row counts alone.
- Existing canonical records were reconciled without destructive cleanup, duplicate external codes, startup seeding, direct persistence, or a hidden reset route.
- Invalid-reference and paging/model failures remain covered by `CrmHrApiIntegrationTests`, which verifies structured failure without partial persistence.

`Pass`.
