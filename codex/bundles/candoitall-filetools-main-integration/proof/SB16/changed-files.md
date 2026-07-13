# SB16 Changed-File SHA-256 Manifest

Date: 2026-07-13.

`Before` uses the Git `HEAD` blob for tracked legacy files, the last trusted governed SB08 hash for the previously untracked authorization owners, and `ABSENT` for new SB16 files/artifacts. `After` is the final verified working-tree SHA-256. The recursive proof manifest is represented by `source-hashes.sha256`; this file does not hash itself.

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/App/CanDoItAll.Web/CanDoItAll.Web.csproj` | `054252e42af0ba13ee74a5bbe542feae80bcc6f1462e36f673e234b41fe9021f` | `edd5fb5b278587cfdf5c1f3afe43e6a543b97e4359d77830ab26809515dd4302` |
| `repo://src/Modules/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj` | `0892e93a1eaacd449100b6fcd346680e7fd46db8dab482b7f90be8b912a48838` | `3286d1da0dfaf69785ac224d01fae4494b52ae14f44132942685d5bb517e319e` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj` | `0ae7c005eaf2aae1d76ebd68820a789068c72260ca1e3c7a520401334bdfb16a` | `955b0fd8332e2a586b0e86b803e696ccb210fc05936ad509633e154cb2bc6db8` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs` | `b619b401300a1e6f70e5d8d392ae279e6a527e27c46142d9503f88c30ca7e787` | `dd0151140f34b897cf7534a710317b765a651208f45856b51d272148806bd69f` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor` | `08e1b6520b25c50998c5513ac55b23a396575bb75c0c5cca81bdabc625b6411e` | `783092e478ad76d726af6d53da5abfecf6bd027a65028054cf8f46cecc941c05` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureOverlayDialog.razor` | `db4138fbf8ee9c647317adf8b7511bcbdbe1ca8093c4d2de18329ad502a22e7c` | `763369d3c2fe2c26bd2d1766e3e390ccc08280ab4ee470be8f7e8d74b2bcd293` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureSupportDialogs.razor` | `96c13163bedb507bc363aa3ae31f5e5e857d4a7538d4561a044d3294f30e178f` | `e43db452058cfe940bda740fcb996b27cad56428b47ab923aa2b36ff50c182f1` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Workflows.cs` | `9413bd86523decd753e966873b634ebb7f3dac4a4c3514cbb28908fd9977041b` | `0200bb7636b7a5ae60cfcd8d8bd95b16e2664329517cadc58f9540baf6befa28` |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor` | `0db4b0542d87a32fd9e3e51b733a564327402a3c25bbb488998d9153652ee8e6` | `b7c00b9bdcc1204f09c7a9f5458dbc2af6e32a1d60867bc345734f67e4a5f0a6` |
| `repo://tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` | `e922890d37b61231fb0f1776d31ea04847bf005ebae7bd3a71d7edf1a8bb56f5` | `aaa0a1591ca5aaa1df2817b5f6fb08db0a91d5944e76ba011553033967f39eae` |
| `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructureGraphAdapterTests.cs` | `d5d2524d068976ff6ed004722ebb98e759e025f81dd5a4028f9e91b66868b92e` | `8f3a0ce8f108211ddee401ce89f561c9fa7e2faa732873d34b4a83584eb9022c` |
| `repo://tests/Support/CanDoItAll.Tests.Support/CanDoItAll.Tests.Support.csproj` | `bf4839323777384356242a0775fb9647278ff951c271b4978a1f03510381ab94` | `c80fff5bcc0ea40d8d7bab8ccdd71da69c2d6c353fc19cc1e42542838ea2f94c` |
| `repo://src/Integration/CanDoItAll.FileTools.Integration/AuthorizedFileSaveTarget.cs` | `d6f32f115ffd166092392b9f50e7adbb2d4e8365a8d17bd59cfe541b5bf48fa6` (SB08 trusted) | `3a6c0a272a6d7a0c2261819188d7229fa7ffea4218c63bca34874827b3881b20` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/FileAccessAuthorizationTests.cs` | `8619cc46670ea697f5eb97bf46d075b63f289f63b91fbaf5788ee604e4affd1e` (SB08 trusted) | `e274afc39ade469ceac693e18fe2f7e89d95eb19ccf700b9c2ff82dc6c652a9a` |
| `components://src/CanDoItAll.Components.Mermaid/wwwroot/js/mermaidDiagram.js` | `fc0fc8bbdafd20a897d36255009574714648171a1247598800b2c22d2bee26cf` | `b6620256eaf01f3e051382bf4372247eb7fbc43f10e7575298d6a1213b545bfb` |
| `components://tests/CanDoItAll.Components.BaseLib.Tests/CanDoItAll.Components.BaseLib.Tests.csproj` | `d408b002b5e7e49fbc32ae150824abac7d1d2e2f156736a4ab9ff82773c8d76d` | `44e56147b5a6779e68548c3ded0d392ab6511896612efb707d37649271868358` |
| `components://tests/CanDoItAll.Components.BaseLib.Tests/VisualizationHardeningTests.cs` | `6349c91e1fd07efaa81b963fc640bd360f19e1a151e81d8ce84218f6c64364e9` | `602cd250b7d88e6805dc0a1b516f0eba6f227fdf59cb1080727628320476f33c` |
| New SB16 coordinator/policy/composition/adapter/dialog/tests, Mermaid 0.1.3 package, proof transcripts, and browser artifacts enumerated in `source-hashes.sha256` | `ABSENT` | exact individual hashes in `source-hashes.sha256` |
