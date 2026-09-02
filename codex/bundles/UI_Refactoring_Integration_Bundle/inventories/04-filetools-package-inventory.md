# FileTools Package Inventory

The package manifest contains exactly nine packages:

1. `CanDoItAll.FileTools.Abstractions`
2. `CanDoItAll.FileTools.Desktop`
3. `CanDoItAll.FileTools.FileBrowser.Core`
4. `CanDoItAll.FileTools.FileBrowser.Components`
5. `CanDoItAll.FileTools.Providers.FileSystem`
6. `CanDoItAll.FileTools.FileInteraction.Core`
7. `CanDoItAll.FileTools.FileInteraction.Components`
8. `CanDoItAll.FileTools.FileInteraction.Markdown`
9. `CanDoItAll.FileTools.FileInteraction.Spreadsheet`

All nine must produce one `.nupkg` and one `.snupkg` at the selected coordinated version.

The package validator must continue to reject:

- Components references,
- main-application references,
- project references outside `src`,
- undeclared package dependencies.
