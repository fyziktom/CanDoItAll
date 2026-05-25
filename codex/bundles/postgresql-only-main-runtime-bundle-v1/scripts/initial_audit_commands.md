# Initial audit commands

Run from repository root:

```powershell
git status
git branch --show-current

rg -n -i "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|sqlitewritecoordination|legacysqlitemigrationbootstrap|snapshotcache|ipfssnapshot" src tests docs

rg -n -i "Microsoft\.Data\.Sqlite|Microsoft\.EntityFrameworkCore\.Sqlite" **/*.csproj

dotnet build .\CanDoItAll.slnx
```
