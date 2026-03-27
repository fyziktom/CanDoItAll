# Requirement Traceability

| Requirement | Source Notes | Planned Owner |
| --- | --- | --- |
| `R001` compact path control replaces raw path lead text | `N001` | `subbundles/01-compact-node-path-and-file-presentation` |
| `R002` file name promoted when path points at a file | `N002` | `subbundles/01-compact-node-path-and-file-presentation` |
| `R003` hover full path plus copy and transient success state | `N001` | `subbundles/01-compact-node-path-and-file-presentation` |
| `R004` non-preview double-click opens quick-action modal | `N003` | `subbundles/02-add-non-preview-double-click-quick-actions` |
| `R005` square action buttons with `Edit` first | `N003` | `subbundles/02-add-non-preview-double-click-quick-actions` |
| `R006` explicit secondary action per node type | `N003` | `subbundles/02-add-non-preview-double-click-quick-actions` |
| `R007` explicit handling for non-editable node types | `N003` | `subbundles/02-add-non-preview-double-click-quick-actions` |
| `R008` settings icon replaces `cfg` | `N004` | `subbundles/03-polish-settings-icon-and-toolbar-safe-offset` |
| `R009` settings overlay stays below toolbar | `N005` | `subbundles/03-polish-settings-icon-and-toolbar-safe-offset` |
| `R010` focused automated proof and screenshots | `N001`-`N005` | All subbundles, finalized in `reviews/01-execution-report.md` |

## Raw Note Closure Matrix

| Raw note | Bundle coverage |
| --- | --- |
| `N001` compact path label, full-path tooltip, copy state | `R001`, `R003`, subbundle 01 |
| `N002` file name shown on node when path ends with file | `R002`, subbundle 01 |
| `N003` non-preview double-click quick-action modal | `R004`, `R005`, `R006`, `R007`, subbundle 02 |
| `N004` settings icon instead of `cfg` | `R008`, subbundle 03 |
| `N005` settings overlay no longer hidden behind toolbar | `R009`, subbundle 03 |
