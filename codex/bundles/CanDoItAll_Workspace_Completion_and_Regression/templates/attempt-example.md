# Campaign attempt format

Create real files in the private evidence root first and replace every example value below. Do not copy an example as passing evidence. Append each new attempt; retain failures. Checkpoint hashes refer to your frozen source manifest, not the ZIP hash.

```json
{
  "attempt_id": "live-files-actual-attempt-1",
  "status": "PASSED",
  "mode": "live-agent",
  "execution": "live",
  "source_commit": "<40 lowercase hex from actual checkout>",
  "checkpoint_sha256": "<64 lowercase hex from frozen source manifest>",
  "command": "<exact safe command or replayable browser procedure>",
  "reason": "",
  "checks": {"ui_driven": true, "owner_readback": true},
  "provider": "<configured provider label without credentials>",
  "model": "<actual configured model>",
  "model_requests": 6,
  "executions": [{"id": "<persisted run ID>", "model_requests": 6}],
  "evidence": [
    {"kind": "ui", "path": "live-files/ui-manifest.json", "sha256": "<actual file hash>"},
    {"kind": "owner", "path": "live-files/owner-manifest.json", "sha256": "<actual file hash>"},
    {"kind": "provider", "path": "live-files/provider-manifest.json", "sha256": "<actual file hash>"}
  ]
}
```

For non-live passing groups use their required mode from `campaign-plan.json`, `execution=executed`, nonempty evidence and the appropriate UI/owner checks. For test-suite groups record discovered/executed/failed counts in the referenced group manifest. A BLOCKED/FAILED/REHEARSAL/NOT_RUN attempt needs a specific reason; if no code ran, use `execution=not-run` and do not claim effect checks.

A genuinely attempted failed live call may still have model usage. Record all consumed requests and executions, including failed retries, so the campaign budget remains auditable. Only accepted source checkpoints can support a final passing group; after a fix, retain earlier checkpoints only with a written, hashed non-invalidation assessment.
