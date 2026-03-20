# Proposed Reason JSON Shapes

## Pair evidence JSON

```json
{
  "pair": {
    "leftIndexedScoreId": 101,
    "rightIndexedScoreId": 245
  },
  "signals": {
    "titleStrictExact": false,
    "titleLooseTokenJaccard": 0.89,
    "composerStrictExact": true,
    "composerAliasMatch": true,
    "catalogSystemLeft": "opus",
    "catalogSystemRight": "opus",
    "catalogValueMatch": true,
    "workNumberMatch": true,
    "movementNumberMatch": null,
    "keyMatch": true,
    "arrangementConflict": false,
    "excerptConflict": false,
    "embeddingCosine": 0.9481
  },
  "decision": {
    "compositeScore": 0.973,
    "confidenceBand": "Definite",
    "needsReview": false
  }
}
```

## Cluster summary JSON

```json
{
  "sharedSignals": {
    "composerStrict": "frederic chopin",
    "catalog": "opus 27 number 2",
    "key": "d_flat_major"
  },
  "warnings": [
    "1 member uses only loose composer alias"
  ],
  "stats": {
    "memberCount": 5,
    "minConfidence": 0.902,
    "maxConfidence": 0.997,
    "avgConfidence": 0.954
  }
}
```

## Manual action audit JSON

```json
{
  "action": "RemoveMembership",
  "reason": "Arrangement should not be in exact-work group",
  "performedBy": "curator",
  "performedUtc": "2026-03-19T12:30:00Z",
  "stickyProtectionCreated": true
}
```

## Guidance

Keep reason JSON:
- compact,
- versionable,
- explicit,
- UI-friendly,
- not dependent on internal object dumps.
