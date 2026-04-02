
# Default Routing Policy

This file is the human-readable policy companion to the workbook sheet `Default_Routing`.

| File subtype / use case | MIME hints | Edit likelihood | Preview likelihood | Publish likelihood | Default storage | Fallbacks | Reasoning |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Docx | application/vnd.openxmlformats-officedocument.wordprocessingml.document | High | Medium | Low | FileSystem | IPFS | Editable office document; favor mutable local storage. |
| Text | text/plain | High | Medium | Low | FileSystem | IPFS | Frequently edited notes/runbooks/log snippets. |
| Json | application/json | High | Medium | Low | FileSystem | IPFS | Configuration and machine-readable editable content. |
| Markdown | text/markdown | High | High | Low | FileSystem | IPFS | Human-authored editable text with preview. |
| Mermaid | text/plain / mermaid source | High | High | Low | FileSystem | IPFS | Diagram source should stay editable. |
| Log | text/plain | Medium | Low | Low | FileSystem | IPFS | Often inspected/updated locally. |
| Excel | application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, text/csv | Medium | Medium | Low | FileSystem | IPFS | Usually edited before final archive. |
| Pdf | application/pdf | Low | High | Medium | IPFS | FileSystem | Immutable review/share artifact. |
| Image | image/* | Low | High | Medium | IPFS | FileSystem | Preview/share optimized immutable asset. |
| Screenshot | image/png,image/jpeg | Low | High | Medium | IPFS | FileSystem | Evidence assets usually immutable after capture. |
| Audio | audio/* | Low | High | Medium | IPFS | FileSystem | Large immutable media. |
| Video | video/* | Low | High | Medium | IPFS | FileSystem | Large immutable media; stream if possible. |
| Archive | application/zip, application/x-7z-compressed | Low | Low | High | IPFS | FTP, FileSystem | Release/archive package; immutable default with publish fallback. |
| Release package | zip/tar.gz/bin package | Low | Low | High | FTP | IPFS, FileSystem | Deployment/publish intent overrides archive default. |
| Evidence export | xlsx,pdf,png | Low | High | Medium | IPFS | FileSystem | Evidence should remain shareable/immutable by default. |
| Prompt export | txt,md,json | Medium | Medium | Low | FileSystem | IPFS | Generated prompts may still be edited locally. |
| Recording media | video/mp4,audio/mpeg | Low | High | Medium | IPFS | FileSystem | Recording nodes already track storageReference. |
| Deployment mirror | folder sync | Low | Low | High | FTP | FileSystem | Explicit publish/deploy path. |
| Unknown | */* | Unknown | Unknown | Unknown | FileSystem | IPFS | Conservative default: editable-first until user overrides. |

## Policy rules Codex must implement

- Recommendations are suggestions with explainable reasons and alternatives, not hard-coded hidden behavior.
- Project- or node-level overrides can beat workspace defaults.
- Capability mismatch must downgrade the recommendation safely. Example: if the recommended IPFS target is disabled or fails connection test, fall back to the first healthy provider that satisfies the required capability set.
- Provider health, read-only mode, size limits, and unsupported actions must be surfaced to the UI and tests.
- Unknown types default conservatively to an editable-first destination until the user overrides or a stronger rule exists.

## Initial capability heuristics

- `FileSystem` should be preferred for mutable editable documents and local-open workflows.
- `IPFS` should be preferred for immutable/shareable preview assets and evidence.
- `FTP` should be preferred for publish/deploy/mirror intent, not general interactive editing.
