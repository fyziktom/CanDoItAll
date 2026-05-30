# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001`: rename exactly `C:\repositories\CanDoItAll\src\CanDoItAll.Components`. | `bundle://requirements/01-normalized-requirements.md#req-001` | `subbundles/01-project-rename-and-reference-repair` | `bundle://proof/SB01/transcripts/stale-reference-search.txt` | Implemented as `CanDoItAll.AppComponents` by repository convention. |
| `N002`: repair projects that use this. | `bundle://requirements/01-normalized-requirements.md#req-002` and `#req-003` | `subbundles/01-project-rename-and-reference-repair` | `bundle://proof/SB01/transcripts/renamed-project-build.txt` and `bundle://proof/SB01/transcripts/component-tests.txt` | Direct web and test consumers are in scope. |
| `N003`: do not touch the components repository. | `bundle://requirements/01-normalized-requirements.md#req-004` | `subbundles/01-project-rename-and-reference-repair` | `bundle://proof/SB01/transcripts/stale-reference-search.txt` | Package and sibling-repo references remain `CanDoItAll.Components.*`. |
