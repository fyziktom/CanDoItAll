# Settings Renderer And Catalog Architecture

## Existing Asset To Reuse

CanDoItAll already has:

- `ConnectorConfigurationSchema`;
- `ConnectorConfigFieldDescriptor`;
- `ConnectorConfigFieldType`;
- `ConnectorConfigState`;
- `ConnectorConfigFieldEditor`.

This should become a canonical settings/schema layer rather than being duplicated for plugins.

## Proposed Extraction

Move or adapt the neutral parts into a shared namespace/project, for example:

```text
CanDoItAll.SharedKernel.Configuration
  ConfigurationSchema
  ConfigurationFieldDescriptor
  ConfigurationFieldType
  ConfigurationState
  ConfigurationSecretRequirement
  IConfigurationSchemaValidator
  ConfigurationValidationResult
```

Keep connector-specific fields such as workbench node hooks in Workspace/Resources.

## Renderer Strategy

### Phase 1: Schema Fallback

All plugins must render through the schema fallback if no custom renderer is registered.

Supported initial fields:

- text;
- URL;
- number;
- boolean;
- JSON;
- select/enum;
- secret reference;
- multiline text.

### Phase 2: Bundled Renderer Components

Bundled plugins may register trusted Blazor components through a renderer registry. The renderer component receives:

- plugin id;
- connection id;
- schema;
- current state;
- validation result;
- secret picker model;
- save/cancel callbacks.

### Phase 3: Remote Renderer Packages

Remote renderer components require a separate review for trust, signatures, compatibility, and sandboxing. Until that is implemented, remote shop plugins use schema fallback.

## Catalog UI

The plugin catalog should show:

- bundled, installed, disabled, unavailable, and shop-available plugins;
- plugin id/version/vendor/source;
- trust level and capabilities;
- installed/enabled state;
- settings/connection count;
- health-check result;
- compatibility warnings;
- executor list.

## Settings UI

The settings page should show:

- plugin-level settings;
- connection/account settings;
- secret bindings;
- health check action;
- executor availability;
- OAuth2 authorization action when the broker is implemented.

## Workflow UI

The workflow node editor should render plugin executor settings through the same renderer host/schema fallback. It must not add `if pluginId == ...` branches.
