# Raw scope excerpts and closure map

The full owner-supplied review remains in this task's 2026-09-05 message. Exact excerpts defining this child:

> repair them and then prepare bundle for next part as architect recommend and implement and test it.

> Tyto dvě změny bych udělala jako malý SB09 nebo jednoduše krátký hardening follow-up. Není důvod znovu otevírat celý sedmifázový bundle.

> Musí existovat jeden autoritativní selected provider.

> Core provider/editor load by měl být fail-closed. Selhání secret katalogu může být explicitní partial failure, pokud se tím nevymaže uložená reference.

> Zde je důležité nekopírovat mechanicky Agents outcome taxonomy.

> Další hlavní bundle by měl být zaměřený výhradně na state/read seam AgentProviderProfilesPanel, bez routingových změn a bez vtažení request history.

Some markup/emphasis in the supplied message is omitted in these short excerpts; the normalized decisions are in review-and-decisions.md. The surrounding owner request authorizes work; quoted advice is evaluated against current source and does not independently authorize unrelated actions.

| Input obligation | Owner | Closure evidence |
|---|---|---|
| Initial catalog/reload overlap | Agents SB09 | Eight focused regression cases, including two delayed-load variants |
| Nested dialog tokens | Agents SB09 | Six dispose/replace variants across three direct dialog kinds; independent dialog preserved |
| Provider state/selection/session | Providers-01 foundation | Direct session tests and controlled public UI events |
| Provider reads/fail-closed/partial metadata | Providers-01 integration | Adapter unit tests, rendering tests and production-composed behavior tests |
| Current functionality | Providers-01 closure | Existing tests, promoted stable lanes, browser section/form/overlay checks |
| Commands/effects | Future Providers-02 | Deferred deliberately; actual commit-boundary analysis required |
| Sandbox/watch checkpoint | Future catalog extraction | Sequence reflected in shared reference; no current speedup claim |
| Request history | Future Provider-History-01 | Internals untouched, lazy host behavior preserved |
