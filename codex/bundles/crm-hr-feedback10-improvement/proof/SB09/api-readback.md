# SB09 Bounded API Readback

Readback date: `2026-07-24`

## Queries

The final Release host was queried through these bounded public API reads:

```text
GET /api/crm-hr/parties?page=1&pageSize=1
GET /api/crm-hr/workforce?page=1&pageSize=1
GET /api/crm-hr/recruiting/applications?page=1&pageSize=20
```

Detailed application workspaces were then read through the corresponding application-detail endpoints to count interviews, lifecycle tasks, support assignments, and conversion state.

## Observed Results

| Projection | Observed result |
| --- | ---: |
| All parties | `78` |
| Demo parties | `29` |
| Workforce records | `32` |
| Recruiting applications | `8` |
| Skill catalog entries | `12` |
| Demo party-skill assignments | `40` |
| Demo capacity blocks | `5` |
| Demo interviews | `9` |
| Demo lifecycle tasks | `4` |
| Candidates with manager/buddy/mentor support | `3` |
| Completed recruiting-to-workforce conversions | `1` |

Application stage distribution:

| Stage | Count |
| --- | ---: |
| Applied | `1` |
| Screening | `1` |
| Interviewing | `2` |
| Offer | `1` |
| Hired | `1` |
| Rejected | `1` |
| Withdrawn | `1` |

Paging checks:

- Directory has a second page at page size `18`; the browser reached `Page 2 / 5`.
- Workforce has a second page at page size `12`; the browser reached `Page 2 / 3`.

## Conclusion

The bounded API projections agree with the populated Directory, Workforce, and Recruiting UI. The distribution is intentionally heterogeneous and includes negative outcomes; no unavailable financial domain was fabricated. `Pass`.
