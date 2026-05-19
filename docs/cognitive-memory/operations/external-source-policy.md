# External Source Policy

External source ingestion accepts uploaded files and HTTP/HTTPS web links as source material. It does not approve memory by itself.

## Limits

| Limit | Value |
| --- | --- |
| Upload size | 10 MB |
| Extracted source text | 1,000,000 characters |
| Chunk size | 4,000 characters |
| Minimum chunk size | 80 characters |

The limits live in `CognitiveMemoryExternalSourceIngestionLimits`.

## Sensitive Content

The ingestion service rejects source text that looks like credential material, including password, secret, API key, token, connection-string assignments, and private-key markers. It also rejects web links with sensitive query parameter names such as `token`, `access_token`, `api_key`, `client_secret`, `password`, or `secret`.

Rejected content creates a failed ingestion operation when the text has already reached `IngestAsync`; source items and evidence anchors are not persisted.

Logs use a safe locator value and must not include raw secret values.

## Extraction Errors

File extraction errors include the file name and the extractor failure message. Website extraction errors include the host. Unsupported or malformed binary data should fail clearly rather than become fake source text.
