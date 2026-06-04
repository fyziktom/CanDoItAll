# Product Tool Provider Extraction

## Project-Structure Tools

Move hard-coded project-structure tool attachment from MAF into the owning module through a provider implementation. Preserve exact tool names and access behavior.

## Image-Generation Tools

Move hard-coded image-generation tool attachment from MAF into an owning provider. If image generation uses provider-native runtime behavior that must stay in MAF, document that as a narrow exception and keep only provider-native adapter logic in MAF.

## Reference Cleanup

After each extraction, run source scans to determine whether MAF still needs references to Projects, Workbench, Workspace, or Security. Remove only references proven unused. Otherwise record allowed-list reasons and next-phase removal path.
