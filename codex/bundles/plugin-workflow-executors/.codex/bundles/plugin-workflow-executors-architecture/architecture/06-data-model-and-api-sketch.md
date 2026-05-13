# Data Model And API Sketch

## Entity Sketch

```csharp
public sealed class PluginInstallationEntity
{
    public Guid Id { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string ManifestSnapshotJson { get; set; } = "{}";
    public string SettingsJson { get; set; } = "{}";
    public DateTimeOffset InstalledAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PluginConnectionEntity
{
    public Guid Id { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AuthKind { get; set; } = "settings";
    public string SettingsJson { get; set; } = "{}";
    public string SecretBindingsJson { get; set; } = "{}";
    public string AuthStateJson { get; set; } = "{}";
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

## API Sketch

```text
GET    /api/plugins/catalog
GET    /api/plugins/installations
POST   /api/plugins/installations
PATCH  /api/plugins/installations/{installationId}
DELETE /api/plugins/installations/{installationId}

GET    /api/plugins/{pluginId}/connections
POST   /api/plugins/{pluginId}/connections
GET    /api/plugins/{pluginId}/connections/{connectionId}
PUT    /api/plugins/{pluginId}/connections/{connectionId}
DELETE /api/plugins/{pluginId}/connections/{connectionId}

POST   /api/plugins/{pluginId}/connections/{connectionId}/health-check

GET    /api/plugins/shop/catalog
POST   /api/plugins/shop/sources
```

## Workflow API Impact

`GET /api/workflows/executor-catalog` should continue to work, but plugin executors should include source/provenance/availability metadata. Saved workflows must remain compatible.
