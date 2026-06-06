# Requirement Traceability

| Requirement | Source | Owning subbundles | Planned proof |
| --- | --- | --- | --- |
| Narrow projection host into module-local facets | `bundle://inputs/02-structured-input.md` | SB05-SB48 | Build, focused tests, source assertions, critical manifests |
| Preserve projection behavior and source-family order | `bundle://requirements/01-normalized-requirements.md` | SB01-SB72 | Focused projection tests and source-order assertions |
| Keep Process Core and production driver APIs out of scope | `bundle://requirements/02-hard-constraints.md` | SB04, SB60, SB64, SB72 | Source scans and gate proof |
| Avoid UI drift | `bundle://requirements/02-hard-constraints.md` | SB04, SB64, SB72 | Source scan proving no UI/Razor/CSS/JS/TS changes |
| Close proof with completed-stage validation | `bundle://requirements/03-acceptance-criteria.md` | SB72 | Completed validator transcript and execution report |

