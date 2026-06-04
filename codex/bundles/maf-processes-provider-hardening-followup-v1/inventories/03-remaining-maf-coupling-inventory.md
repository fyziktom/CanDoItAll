# Remaining MAF Coupling Inventory

| Dependency | Current project reference evidence | Expected treatment |
| --- | --- | --- |
| `CanDoItAll.Modules.Projects` | `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | Try to remove after project-structure provider extraction if unused. |
| `CanDoItAll.Modules.Workbench` | same | Try to remove after project-structure provider extraction if unused. |
| `CanDoItAll.Modules.Workspace` | same | Likely allowed for workspace runtime tools; document exact reason. |
| `CanDoItAll.Modules.Security` | same | Inventory source usage; remove only if unused. |
| `CanDoItAll.Tools.Documents` | same | Not part of process decoupling; inventory later if document tools become providers. |
| `ExcelDataReader`, `PdfPig` packages | MAF csproj | Likely document/tool behavior; not targeted unless owned by provider migration. |
