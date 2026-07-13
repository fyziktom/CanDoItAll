# SB02 restore after package update
Working directory: C:\repositories\CanDoItAll
Command: dotnet restore CanDoItAll.slnx
Timestamp: 2026-07-07T20:33:26.7311540-04:00

  Zjišťují se projekty, které se mají obnovit...
C:\repositories\CanDoItAll\tools\App\CanDoItAll.Manager\CanDoItAll.Manager.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\tests\Support\CanDoItAll.Tests.Support\CanDoItAll.Tests.Support.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\tools\Seeding\CanDoItAll.ScenarioSeeder\CanDoItAll.ScenarioSeeder.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj : warning NU1903: Balíček „Microsoft.OpenApi“ 2.0.0 má známé vysoké ohrožení zabezpečení závažnosti, https://github.com/advisories/GHSA-v5pm-xwqc-g5wc [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj : error NU1605: Upozornění jako chyba: Zjistil se downgrade balíčku: Microsoft.Extensions.DependencyInjection.Abstractions z 10.0.9 na 10.0.7. Pokud chcete vybrat jinou verzi, odkazujte na balíček přímo z projektu.  [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj : error NU1605:  CanDoItAll.AgentFramework.Workflows.MafAdapter -> Microsoft.Agents.AI 1.13.0 -> Microsoft.Extensions.DependencyInjection.Abstractions (>= 10.0.9)  [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj : error NU1605:  CanDoItAll.AgentFramework.Workflows.MafAdapter -> Microsoft.Extensions.DependencyInjection.Abstractions (>= 10.0.7) [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj : error NU1605: Upozornění jako chyba: Zjistil se downgrade balíčku: Microsoft.Extensions.AI.Abstractions z 10.6.0 na 10.5.1. Pokud chcete vybrat jinou verzi, odkazujte na balíček přímo z projektu.  [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj : error NU1605:  CanDoItAll.AgentFramework.Workflows.MafAdapter -> Microsoft.Agents.AI 1.13.0 -> Microsoft.Extensions.AI.Abstractions (>= 10.6.0)  [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj : error NU1605:  CanDoItAll.AgentFramework.Workflows.MafAdapter -> Microsoft.Extensions.AI.Abstractions (>= 10.5.1) [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Hosting\CanDoItAll.AgentFramework.Hosting.csproj : error NU1605: Upozornění jako chyba: Zjistil se downgrade balíčku: Microsoft.Extensions.DependencyInjection.Abstractions z 10.0.9 na 10.0.7. Pokud chcete vybrat jinou verzi, odkazujte na balíček přímo z projektu.  [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Hosting\CanDoItAll.AgentFramework.Hosting.csproj : error NU1605:  CanDoItAll.AgentFramework.Hosting -> Microsoft.Agents.AI.Hosting.A2A 1.13.0-preview.260703.1 -> Microsoft.Extensions.DependencyInjection.Abstractions (>= 10.0.9)  [C:\repositories\CanDoItAll\CanDoItAll.slnx]
C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Hosting\CanDoItAll.AgentFramework.Hosting.csproj : error NU1605:  CanDoItAll.AgentFramework.Hosting -> Microsoft.Extensions.DependencyInjection.Abstractions (>= 10.0.7) [C:\repositories\CanDoItAll\CanDoItAll.slnx]
  Obnovil se projekt C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj (v 4,09 s).
  Obnovení projektu C:\repositories\CanDoItAll\src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj (v 4,19 s) bylo neúspěšné.
  Obnovil se projekt C:\repositories\CanDoItAll\tools\Diagnostics\CanDoItAll.OpenAiContextProbe\CanDoItAll.OpenAiContextProbe.csproj (v 4,2 s).
  Obnovení projektu C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Hosting\CanDoItAll.AgentFramework.Hosting.csproj (v 4,11 s) bylo neúspěšné.
  Obnovil se projekt C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj (v 4,23 s).
  Obnovil se projekt C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj (v 4,1 s).
  Obnovil se projekt C:\repositories\CanDoItAll\tests\Support\CanDoItAll.Tests.Support\CanDoItAll.Tests.Support.csproj (v 4,23 s).
  Obnovil se projekt C:\repositories\CanDoItAll\tests\Memory\CanDoItAll.Memory.Tests\CanDoItAll.Memory.Tests.csproj (v 4,14 s).
  Obnovil se projekt C:\repositories\CanDoItAll\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj (v 4,14 s).
  Obnovil se projekt C:\repositories\CanDoItAll\src\App\CanDoItAll.Composition\CanDoItAll.Composition.csproj (v 4,23 s).
  Obnovil se projekt C:\repositories\CanDoItAll\tools\Seeding\CanDoItAll.ScenarioSeeder\CanDoItAll.ScenarioSeeder.csproj (v 4,09 s).
  Obnovil se projekt C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj (v 4,15 s).
  Obnovil se projekt C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj (v 4,15 s).
  Obnovil se projekt C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj (v 4,23 s).
  Obnovil se projekt C:\repositories\CanDoItAll\tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj (v 4,15 s).
  Obnovil se projekt C:\repositories\CanDoItAll\src\Foundation\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj (v 4,16 s).
  78 z 94 projektů jsou v aktuálním stavu pro obnovení.
ExitCode: 1
