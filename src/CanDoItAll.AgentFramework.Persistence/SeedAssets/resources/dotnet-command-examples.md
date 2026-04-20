Scaffold the app in the parent folder:
- tool: workspace_dotnet_new
- template: blazor
- name: SimpleCalculatorApp
- parentDirectory: <scenario-parent-folder>

Build the generated app:
- tool: workspace_dotnet_build
- targetPath: SimpleCalculatorApp.csproj
- workingDirectory: <scenario-parent-folder>\SimpleCalculatorApp
- configuration: Debug
