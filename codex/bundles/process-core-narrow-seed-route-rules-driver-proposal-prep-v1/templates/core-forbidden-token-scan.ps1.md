# Core Forbidden Token Scan Template

```powershell
$forbidden = @(
  'DbContext',
  'IDbContextFactory',
  'Microsoft.EntityFrameworkCore',
  'CanDoItAll.Modules.Processes',
  'CanDoItAll.Infrastructure',
  'CanDoItAll.AgentFramework',
  'Workspace',
  'Storage',
  'ProcessRunAutomationDispatchService',
  'IProcessDriver',
  'DriverPack',
  'DriverRegistry',
  'IServiceProvider',
  'IServiceScopeFactory'
)

foreach ($token in $forbidden) {
  rg -n $token src/CanDoItAll.Processes.Core
  if ($LASTEXITCODE -eq 0) { throw "Forbidden token found in Core: $token" }
}
```
