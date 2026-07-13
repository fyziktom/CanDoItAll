# SB02 package diff
Working directory: C:\repositories\CanDoItAll
Command: git diff -- package-update project files
Timestamp: 2026-07-07T20:33:13.3460841-04:00

warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj', LF will be replaced by CRLF the next time Git touches it
diff --git a/src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj b/src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj
index 9cbd42cf2..4d1a6ab53 100644
--- a/src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj
+++ b/src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj
@@ -12,7 +12,7 @@
   </ItemGroup>
 
   <ItemGroup>
-    <PackageReference Include="Microsoft.Agents.AI.Hosting.A2A" Version="1.8.0-preview.260528.1" />
+    <PackageReference Include="Microsoft.Agents.AI.Hosting.A2A" Version="1.13.0-preview.260703.1" />
     <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.7" />
   </ItemGroup>
 
diff --git a/src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj b/src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
index 955f43c71..fd94d9948 100644
--- a/src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
+++ b/src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
@@ -23,11 +23,11 @@
 
   <ItemGroup>
     <PackageReference Include="Azure.AI.OpenAI" Version="2.9.0-beta.1" />
-    <PackageReference Include="Microsoft.Agents.AI" Version="1.8.0" />
-    <PackageReference Include="Microsoft.Agents.AI.A2A" Version="1.8.0-preview.260528.1" />
+    <PackageReference Include="Microsoft.Agents.AI" Version="1.13.0" />
+    <PackageReference Include="Microsoft.Agents.AI.A2A" Version="1.13.0-preview.260703.1" />
     <PackageReference Include="Microsoft.Agents.AI.Mem0" Version="1.0.0-preview.251028.1" />
-    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.8.0" />
-    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.8.0" />
+    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.13.0" />
+    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.13.0" />
     <PackageReference Include="ModelContextProtocol" Version="1.1.0" />
     <PackageReference Include="OllamaSharp" Version="5.4.25" />
     <PackageReference Include="OpenTelemetry.Api" Version="1.15.3" />
diff --git a/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj b/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj
index a3b345aa7..05a31f815 100644
--- a/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj
+++ b/src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj
@@ -11,8 +11,8 @@
   </ItemGroup>
 
   <ItemGroup>
-    <PackageReference Include="Microsoft.Agents.AI" Version="1.8.0" />
-    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.8.0" />
+    <PackageReference Include="Microsoft.Agents.AI" Version="1.13.0" />
+    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.13.0" />
     <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.5.1" />
     <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.7" />
   </ItemGroup>
ExitCode: 0
