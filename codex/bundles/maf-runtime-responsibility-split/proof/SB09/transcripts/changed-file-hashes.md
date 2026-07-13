Command: PowerShell Get-FileHash SHA256 plus git rev-parse HEAD:path for SB09 changed files
ExitCode: 0

# SB09 Changed File Hashes

| Path | Baseline Git Blob | Current SHA-256 |
| --- | --- | --- |
| `Templates/Capabilities/manifest.json` | `92617a9880b5f15a74bab4495b4dca8c81396090` | `53608ac735e0a9a4539b5ffa417de385c37a9c9e10b3bde23f8a36b71a866617` |
| `Templates/Capabilities/mcps.json` | `345631918df18a70fa1405f211e656f4ace06cef` | `49c5806d96249cb37480a5e3856512685110d8037828a09fd0e97136b1d921c2` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs` | `absent` | `0f374812721ba043349fc2eeaa653f817e1123e2c575e25ce75c38bf57a0adda` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs` | `ea4706e02100cd07e92c2f7a494ff222c4d9f2da` | `4c0efdd94ae6f75fc35eb541e1ebef09ad3564d26913d74d9801d9d0eb0ff255` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | `9b7f2c2341b44efcd4becf7bd1175d1074438d4f` | `338e054798f4e6a41ec1098aeef3888e77ed710c2a125cdffb6c5e09ddd43cc2` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | `9abcead0c5da076993634ea4177bef9c665b3ffe` | `17bdc293dce86fb039042f81503263a48b97dfaa13eb61435c2c28a61dc78526` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs` | `9207f19f4d8d7da1112731781fb2f790b9816f1f` | `e469adc184c65db681a27db6862a5e4489dab711f79801ad5f5f8e5c841095a2` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/Seeds/ManagedSeedProviderFallbacks.cs` | `0c97fa2d573020216a8d75849b4e3c58704ddf88` | `2ca735dfd5af03cb211c0cfbc34e3314a258cf721344b92b1dd8ebce1edc347e` |
| `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp.Abstractions/Mcp.cs` | `70cfc2576f619e91134d0698d154d62a564b1948` | `d6bdd01b41caa862c66c9893b9f3e556107991ad64dc788f22a80e142e20195e` |
| `src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpClientFactory.cs` | `52e53feee58a23336d1583198b6444726f9135ba` | `5df5e78259d0fba2a5d3ab5fef2887cbf494e82be34e607ea5977382fedb5bea` |
| `tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs` | `283a23bdf50d90103960618e7072f460ad716627` | `9d14c4652380957cb7cf836d1e447d8b3ba6eb91c20f7c3457f4acad09404b11` |
| `tests/Unit/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs` | `20ef19311dae6b587d13a4e72ad392c78afe6af9` | `505d82708db619fa182f4407266ef522469f92233d47055aa019c9a5cf64cb5d` |
| `tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | `e29563f6468167e1213b79d0bc4bcc0230b1db71` | `69d18dcaf0724f129e75b99beee3c4910839517440e884f92df97599d3577d54` |
| `tests/Unit/CanDoItAll.Tests.Unit/ManagedSeedProviderFallbacksTests.cs` | `b0fcd7b70179433768cedcc1bad1ac5d5faf9cc4` | `5375f744cb218178b2923a7082a37bc0bd5c394a6f16677556107f6f24d4c0a9` |
