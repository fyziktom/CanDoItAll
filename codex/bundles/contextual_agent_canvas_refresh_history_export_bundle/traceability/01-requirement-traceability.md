# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001/R001 automatic refresh after contextual agent changes in project/process windows | `requirements/01-normalized-requirements.md` | `subbundles/01-canvas-refresh-callback` | targeted component/build tests plus browser route proof | Critical foundation |
| N002/R002/R003 preserve canvas location, zoom, and open floating windows | `architecture/01-target-solution.md` | `subbundles/01-canvas-refresh-callback` | capture live state before reload and browser verification | Must not reset state |
| N003/R004/R005 tiny history icon and latest 25 thread dialog | `requirements/01-normalized-requirements.md` | `subbundles/02-thread-history-dialog` | component test and open-dialog browser proof | No nested buttons |
| N004/R006 double-click thread opens floating agent chat on that thread | `requirements/01-normalized-requirements.md` | `subbundles/02-thread-history-dialog` | component/dialog result test and browser interaction | Keyboard activation should also work |
| N005/R007/R008 JSON debug export with tool/runtime history | `requirements/01-normalized-requirements.md` | `subbundles/03-thread-history-json-export` | payload-shape test/build and browser button proof | Latest 25 sessions |
