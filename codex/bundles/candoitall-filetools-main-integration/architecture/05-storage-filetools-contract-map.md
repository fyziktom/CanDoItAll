# Storage To FileTools Contract Map

Infrastructure models remain native. The outer Integration adapter performs this mapping and owns all FileTools references.

| Infrastructure-native concept | FileTools target | Mapping rule |
| --- | --- | --- |
| provider/binding + semantic scope | `FileBrowserSourceId` / descriptor | stable bounded ID derived from typed binding/scope identity; no secret/path text |
| storage locator occurrence | `FileBrowserItemKey` | opaque occurrence key scoped to source; UI never parses it |
| directory/object kind | item kind/category/child state | provider facts only; do not infer container from extension |
| display name/relative display path | item name/display path | renderer-safe and root-relative; no absolute roots |
| media type/size/timestamps | optional item metadata | exact/approximate/expensive state reported honestly |
| browse capability flags | FileTools source/item capabilities | intersection of driver, binding config, semantic scope, and current authorization |
| native page/cursor/consistency | `FileBrowserPage` continuation/consistency | cursor remains opaque and query-bound; stale cursor becomes typed provider error |
| native search | `IFileBrowserSearchProvider` | implement only when the provider truly supports it; otherwise FileBrowser progressive search policy is explicit |
| current native read | `IFileContentSource` | available only after current handle authorization; independent of browser session |
| native write + revision | FileInteraction awaited save target | expected revision required where supported; overwrite is separately authorized |

## Native Browse Minimum

`IStorageBrowseDriver` must support bounded root/path/browse facts sufficient to implement FileTools `GetRootAsync`, `GetPathAsync`, and `BrowseAsync`. Native search/stat are optional facets or explicit capabilities; do not use nullable delegates or exception probing as capability discovery.

## Paging

- Page size is bounded by request, provider maximum, and binding settings.
- Continuation tokens are opaque, tamper-detectable or server-retained as appropriate, bound to normalized query/source/revision, and never contain credentials.
- A provider unable to guarantee consistent continuation reports a changed/stale result rather than mixing old/new pages.

## Errors And Logging

Map expected provider failures to typed native errors, then renderer-safe FileTools errors. Log provider kind, storage ID, scope ID, operation, and correlation ID. Mask endpoint credentials, full paths, locator tokens, query secrets, and content.

## Provider Policy

- Filesystem: shallow browse, fresh stat/read, no provider cache, no reparse traversal.
- IPFS CID/DAG: immutable-version capable when driver proves the CID; MFS is mutable.
- FTP: shallow browse only when transport can distinguish entries reliably; unsupported search/metadata fails explicitly.
- Aggregate project/resource scopes are outer providers over one or more native sources; they are not new Infrastructure storage providers.
