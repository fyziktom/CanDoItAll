# SB03 Database Transcript

## Reset Target

Resolved development connection:

```text
Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall;Include Error Detail=true
```

Guard:

```powershell
if ($databaseName -ne 'candoitall_development') { throw ... }
```

## Reset Commands

```powershell
psql -h 127.0.0.1 -p 5432 -U candoitall -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS candoitall_development WITH (FORCE);"
psql -h 127.0.0.1 -p 5432 -U candoitall -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE candoitall_development;"
```

Result:

```text
DROP DATABASE
CREATE DATABASE
```

## Migration Command

```powershell
dotnet ef database update --connection "Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall;Include Error Detail=true" --project src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project src\CanDoItAll.Web\CanDoItAll.Web.csproj --context AppDbContext
```

Result:

```text
Done.
```

## Verification

```text
applied_migrations: 63
tables: 250
CognitiveMemory_AutomationSettings.IsEnabled: boolean, default true, nullable NO
```
