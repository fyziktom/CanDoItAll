# Atomic Update Measurements

## Final conclusion

Atomic publish was reliable for final validation, but slower than the watch lane and sensitive to stale published runtimes that still held file locks.

## Measured atomic runs

1. First atomic commit
   - Start: `2026-03-24T23:40:58.8483893-04:00`
   - End: `2026-03-24T23:43:00.7468519-04:00`
   - Duration: about `121.9s`
   - Transaction: `txn_24183390dce340c08e00b9539ae6d88b`
   - Session: `app_54121c1483524b0f885b1c011ad0a5bd`
   - URL: `http://127.0.0.1:5504`

2. Second atomic commit
   - Start: `2026-03-24T23:45:39.6414318-04:00`
   - End: `2026-03-24T23:47:28.0340450-04:00`
   - Duration: about `108.4s`
   - Transaction: `txn_5e96a24d520448e9b155d49e76defbbd`
   - Session: `app_626c15520ff74c518c47f28f7dde15bf`
   - URL: `http://127.0.0.1:5505`

3. Retry after stale-slot cleanup
   - Previous attempts failed with `Access to the path 'CanDoItAll.ComponentKit.dll' is denied.`
   - Stale published session stopped: `app_54121c1483524b0f885b1c011ad0a5bd`
   - Start: `2026-03-24T23:58:13.5843296-04:00`
   - End: `2026-03-25T00:00:10.3996737-04:00`
   - Duration: about `116.8s`
   - Transaction: `txn_37fc3b5ee6fd4ca2b7f01b4840408490`
   - Session: `app_d14556f581cc4596a2630e8a974d0db4`
   - URL: `http://127.0.0.1:5504`

4. Final atomic commit for the finished compact-label layout
   - Start: `2026-03-25T00:16:56.2996206-04:00`
   - End: `2026-03-25T00:18:52.0916834-04:00`
   - Duration: about `115.8s`
   - Transaction: `txn_185584268bd24d61ae58adb03edab3a9`
   - Session: `app_97a5696b016244709295cd5e174915ab`
   - URL: `http://127.0.0.1:5505`

## Atomic validation result

- Final published runtime rendered the updated compact labels correctly.
- Final published screenshots and metrics:
  - `artifacts/after-atomic-visible-tags/desktop.png`
  - `artifacts/after-atomic-visible-tags/laptop.png`
  - `artifacts/after-atomic-visible-tags/mobile.png`
  - `artifacts/after-atomic-visible-tags/metrics.json`

## Operational issues discovered

- Stale published slot processes can block later atomic updates with file-lock errors.
- `keepPreviousRuntimeWarm=false` was not sufficient on its own to avoid the stale lock.
- Manual stale-session cleanup was required before a later atomic retry could succeed.
