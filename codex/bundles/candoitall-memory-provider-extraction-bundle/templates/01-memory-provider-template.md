# Memory Provider Template

## HTTP provider profile example

```json
{
  "providerInstanceId": "business-memory-dev",
  "displayName": "Business Memory Dev",
  "providerKind": "memory.http",
  "driverKind": "http",
  "protocolVersion": "memory-protocol.v1",
  "enabled": true,
  "capabilities": [
    "context.query.sync",
    "context.query.async",
    "ingestion.snapshot",
    "feedback.delayed"
  ],
  "interactionSupport": {
    "supportsSynchronousQueries": true,
    "supportsAsynchronousOperations": true,
    "supportsSourceRequests": true,
    "supportsFeedback": true,
    "supportsProviderEvents": false
  },
  "selectionTags": ["business", "analysis"],
  "limits": {
    "maxContextSections": 12,
    "maxSourceItems": 100,
    "maxInFlightOperations": 4,
    "operationTimeoutSeconds": 600
  },
  "timeoutPolicy": {
    "connectTimeoutSeconds": 15,
    "operationTimeoutSeconds": 600,
    "pollIntervalSeconds": 5
  },
  "retentionPolicy": {
    "operationTtlDays": 30,
    "feedbackTtlDays": 180,
    "snapshotToIpfs": false
  },
  "uiSurfaces": [
    {
      "kind": "iframe",
      "name": "Provider Console",
      "urlSettingKey": "Memory:Providers:business-memory-dev:ConsoleUrl"
    }
  ],
  "extensions": {
    "provider.vendor.routingTier": "standard"
  }
}
```

## Native Cognitive Memory provider profile example

```json
{
  "providerInstanceId": "native-cognitive-memory-local",
  "displayName": "Native Cognitive Memory Local",
  "providerKind": "memory.native-remote",
  "driverKind": "nativeRemote",
  "protocolVersion": "memory-protocol.v1",
  "enabled": false,
  "capabilities": [
    "context.query.sync",
    "context.query.async",
    "ingestion.snapshot",
    "feedback.immediate",
    "feedback.delayed",
    "events.provider-push",
    "native.probe",
    "native.review-queue"
  ],
  "interactionSupport": {
    "supportsSynchronousQueries": true,
    "supportsAsynchronousOperations": true,
    "supportsSourceRequests": true,
    "supportsFeedback": true,
    "supportsProviderEvents": true
  },
  "selectionTags": ["native", "cognitive-memory"],
  "limits": {
    "maxContextSections": 24,
    "maxSourceItems": 500,
    "maxInFlightOperations": 2,
    "operationTimeoutSeconds": 900
  },
  "timeoutPolicy": {
    "connectTimeoutSeconds": 15,
    "operationTimeoutSeconds": 900,
    "pollIntervalSeconds": 5
  },
  "retentionPolicy": {
    "operationTtlDays": 30,
    "feedbackTtlDays": 365,
    "snapshotToIpfs": false
  },
  "uiSurfaces": [
    {
      "kind": "rcl",
      "name": "Review Queue",
      "componentKey": "native.cognitiveMemory.reviewQueue"
    }
  ],
  "extensions": {
    "native.cognitiveMemory.reviewQueue": true
  }
}
```

Implementation must convert these examples into strongly typed `MemoryProviderProfile`, `MemoryProviderManifest`, selection policy, validation, and provider profile persistence. The native profile must remain disabled unless explicitly configured; it is not a fallback for zero-provider startup.
