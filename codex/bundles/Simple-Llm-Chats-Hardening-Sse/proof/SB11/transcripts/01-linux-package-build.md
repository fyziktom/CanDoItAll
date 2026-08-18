# Linux package build

Host: Ubuntu 24.04.4 LTS x64, Linux 6.18.33.2, .NET SDK 10.0.302. Repository mounted read-only;
artifacts and NuGet state stayed inside disposable container `candoitall-sb11-proof`.

| Command | Exit | Result |
|---|---:|---|
| `docker exec candoitall-sb11-proof dotnet build /src/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release --artifacts-path /tmp/sb11-artifacts -p:UseLocalCanDoItAllLibraries=false -nologo -v:minimal /m:1` | 1 | Cold nuget.org-only restore failed with NU1101 for unpublished `CanDoItAll.FileTools.FileInteraction.Spreadsheet`; 0 warnings, 6 restore errors, 1:38.94. |
| `docker exec candoitall-sb11-proof dotnet pack /dependencies/CanDoItAll.FileTools/src/CanDoItAll.FileTools.FileInteraction.Spreadsheet/CanDoItAll.FileTools.FileInteraction.Spreadsheet.csproj --configuration Release --output /tmp/sb11-feed -p:PackageVersion=0.1.18 -nologo -v:minimal` | 0 | Clean sibling commit `c95dd07208a6d48724443317cdc6cfe67a13020a` produced exact package 0.1.18 in a container-only feed. |
| `docker exec candoitall-sb11-proof dotnet build /src/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release --artifacts-path /tmp/sb11-artifacts -p:UseLocalCanDoItAllLibraries=false -p:RestoreConfigFile=/tmp/SB11.NuGet.Config -nologo -v:minimal /m:1` | 0 | Package-reference Web graph built on Linux with 0 warnings, 0 errors in 4:35.84. |

The diagnostic package proves the package-reference graph and Linux compilation; it does not publish
or mutate an external feed. The first command is retained as the exact SB13 feed prerequisite.
