## Plugin-first connector platform

### Current foundation that is already good
- connector manifests exist
- plugin registries exist
- resource connector plugins already show the right direction

### What still needs to change
- provider/resource pages must stop using legacy enums as the main editor driver
- config editors should render from connector schema metadata
- plugin family/category/grouping should come from the manifest
- provider resolution should be plugin-key first
- legacy enums should become optional compatibility aliases, not primary control flow

### Practical target
A new connector should be addable by:
1. adding a plugin implementation,
2. registering it in DI,
3. optionally adding tests,
4. without expanding a core enum or switch-rendered editor page.
