# Performance And Direct File Follow-up

## Raw Follow-up

> it looks good. use analyzing-dotnet-performance and optimizing-dotnet-performance to assure we are not using and will not use some .net antipatterns. in larger use of our app there might be lots of files, so it must be well designed.
>
> in project structure for assets nodes like image, pdf we have function that it will open them in dialog on doubleclick. this must be preserved, just in that dialog it will use our file interaction component. it must use it without filebrowser when it is opening just one file to assure enough speed. filebrowser must be opened only when we expect browsing of multiple files.
>
> remove bundles from gitignore so we can track it in repo.
>
> improve bundle, do not start implementation yet.

## Normalized Notes

- `N015` — Large file sets must remain bounded in work, memory, I/O, and rendered state; UI pagination without provider-side bounds is insufficient.
- `N016` — Existing Project Structure asset-node double-click behavior must continue to open its dialog.
- `N017` — A known single file must open FileInteraction directly; constructing or loading FileBrowser is forbidden on that path. FileBrowser is reserved for collection/container discovery.
- `N018` — Bundle artifacts must be visible to Git and remain preparation-only in this run.

No note is marked solved during preparation. Execution closes behavior and performance notes from measured proof.
