# Test impact map

## Unit project topics

Planned classes/topics:

- `SharedProviderAccessContextTests`
- `SharedProviderRoutingModelIdTests`
- `SharedProviderCatalogProjectionTests`
- `SharedProviderPublicationPolicyTests`
- `SharedProviderCapabilityIntersectionTests`
- `SharedProviderWireValidationTests`
- `SharedProviderErrorMappingTests`
- `SharedProviderSourceUriPolicyTests`
- `SharedProviderReconciliationTests`
- `SharedProviderUsageExtractionTests`
- `SharedProviderRuntimeProfileMaterializerTests`
- `SharedProviderArchitectureGuardrailTests`

Reuse/extend current provider tests where ownership matches:

- `ProviderProfileSaveValidationTests`
- `ProviderArchitectureFoundationTests`
- `ProviderFeatureMatrixTests`
- `ProviderCatalogProjectionFailureTests`
- `ConnectorPluginRegistryTests`
- `PluginWaveArchitectureGuardrailTests`

## Integration project topics

- `SharedProviderPersistenceIntegrationTests`
- `SharedProviderCatalogApiIntegrationTests`
- `SharedProviderAuthorizationIntegrationTests`
- `SharedProviderOpenAiCompatibilityIntegrationTests`
- `SharedProviderStreamingIntegrationTests`
- `SharedProviderImageIntegrationTests`
- `SharedProviderAccessContextIntegrationTests`
- `SharedProviderRuntimeProjectionIntegrationTests`
- `SharedProviderOpenApiIntegrationTests`
- `SharedProviderCompositionIntegrationTests`

Reuse:

- `ApiAccessAuthorizationIntegrationTests`
- `ApiDocumentationIntegrationTests`
- `ApiStreamingTransportTests`
- `WorkspaceProviderCapabilityIntegrationTests`

## Component topics

- `SharedProviderPublicationPanelTests`
- `SharedProviderSourceManagementTests`
- `SharedProviderCatalogImportDialogTests`
- `SharedProviderImportedProfileEditorTests`

## Playwright topics

- central publish/unpublish;
- source add/test;
- discover/select/import;
- hybrid provider list;
- imported read-only fields;
- unauthorized/offline/identity mismatch;
- overlay screenshot/focus;
- no secret value after save.

## Docker lane

One named orchestrator scenario set:

`SharedProviderMultiInstanceE2E`

It is not hidden inside the normal Integration project. It must report individual scenario
results and container/log artifacts.

## Expected discovery policy

Every subbundle `test-selection.json` declares planned test method names/counts. Before first
execution Codex must:

1. implement or select the exact tests;
2. run `--list-tests`;
3. update planned count only with a written reason;
4. reject zero;
5. record actual in proof.
