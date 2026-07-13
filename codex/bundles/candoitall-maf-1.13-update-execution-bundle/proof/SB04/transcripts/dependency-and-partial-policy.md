CHECK: ProjectReference changes
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj', LF will be replaced by CRLF the next time Git touches it
RESULT: PASS
No ProjectReference changes in csproj diff.

CHECK: PackageReference-only project file changes
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj', LF will be replaced by CRLF the next time Git touches it
9:-    <PackageReference Include="Microsoft.Agents.AI.Hosting.A2A" Version="1.8.0-preview.260528.1" />
10:-    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.7" />
11:+    <PackageReference Include="Microsoft.Agents.AI.Hosting.A2A" Version="1.13.0-preview.260703.1" />
12:+    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9" />
23:     <PackageReference Include="Azure.AI.OpenAI" Version="2.9.0-beta.1" />
24:-    <PackageReference Include="Microsoft.Agents.AI" Version="1.8.0" />
25:-    <PackageReference Include="Microsoft.Agents.AI.A2A" Version="1.8.0-preview.260528.1" />
26:+    <PackageReference Include="Microsoft.Agents.AI" Version="1.13.0" />
27:+    <PackageReference Include="Microsoft.Agents.AI.A2A" Version="1.13.0-preview.260703.1" />
28:     <PackageReference Include="Microsoft.Agents.AI.Mem0" Version="1.0.0-preview.251028.1" />
29:-    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.8.0" />
30:-    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.8.0" />
31:+    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.13.0" />
32:+    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.13.0" />
33:     <PackageReference Include="ModelContextProtocol" Version="1.1.0" />
34:     <PackageReference Include="OllamaSharp" Version="5.4.25" />
35:     <PackageReference Include="OpenTelemetry.Api" Version="1.15.3" />
44:-    <PackageReference Include="Microsoft.Agents.AI" Version="1.8.0" />
45:-    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.8.0" />
46:-    <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.5.1" />
47:-    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.7" />
48:+    <PackageReference Include="Microsoft.Agents.AI" Version="1.13.0" />
49:+    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.13.0" />
50:+    <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.6.0" />
51:+    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9" />

CHECK: New runtime partial files
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs', LF will be replaced by CRLF the next time Git touches it
3:src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs
5:tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs
