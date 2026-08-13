[CmdletBinding()]
param(
    [switch] $Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$storageId = [Guid] 'b7baad3c-e218-47fa-bdab-bccc48943e17'
$sourceRoot = 'C:\Users\lucys\AppData\Local\CanDoItAll\workspace'
$activeRoot = 'C:\Users\lucys\AppData\Local\CanDoItAll\workspace\runtime-overrides\ff24611dad478ec960349d9ad11d1017'
$expectedHostBindingId = 'a20dc0a691234f7e1e6746c63c0d897166bfa64fb10551dca5ad54496cf658c5'
$manifest = @'
[
  {
    "bindingId": "0d8730ea-24b5-446c-8976-4c6fdc6963e9",
    "nodeKey": "custom:384d8e1c9bcc4047954e7934889d7635",
    "locator": "managed-files/project-media/files/9ec6370974634aac8b6eee044d2c6770/agent-project-structure-hardening-db6fca1040bb4a52ba88695328418ad4.mmd",
    "length": 316,
    "sha256": "4e2af87053c6ed366c958ace36e2e11f247e18080503363df2534facf271cab5",
    "oldFingerprint": null,
    "newFingerprint": null
  },
  {
    "bindingId": "108f86e2-aa78-4518-8246-5115792db61c",
    "nodeKey": "custom:feba0a1dfe084e56b36d47dc2db9b94e",
    "locator": "managed-files/project-media/images/be2ebfd7776643f99b2e8051d0b0d99d/calculator-repair-baseline-d8e58a03811a4739b705c142a557984a.png",
    "length": 146392,
    "sha256": "82acef04aa1657012a5919c80c9e42cd7fbb2b915ef1eca92f8d1c75489f5675",
    "oldFingerprint": "1e467c5d5d567758e7ef017db81ee89f077d7e9455264edb77246b27988fe29d",
    "newFingerprint": "cafd0fcf2af027e049840097a8d65dc2854214bd606f64c957396657a12a2ab1"
  },
  {
    "bindingId": "15a804be-b26a-45bb-8d4e-c4cbfeba091f",
    "nodeKey": "custom:dbd84afeb3ef4f17a2b5c1334b5e9830",
    "locator": "managed-files/project-media/files/3324868f66e2478abb8f14f32a5db1e9/main-architecture-overview-c818be7616eb4bbea098a10cae1d3ad7.mmd",
    "length": 1652,
    "sha256": "7c9d2e9f03af1d22284f87bea018c55dda1493a770b7fa64571a61c1edddffb1",
    "oldFingerprint": null,
    "newFingerprint": null
  },
  {
    "bindingId": "2161f93e-f8b9-4d0a-aa92-926b2b3a64d9",
    "nodeKey": "custom:620a9f84c37b487385471967a8713252",
    "locator": "managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72/x-ray-machine-agent-quotation-list2018-e1d6363d36484d81b06dad6ea06d9af8.pdf",
    "length": 1753819,
    "sha256": "5fb7b3d85554bd2ee0b16eca58e7c6e57280e8d064547846035222a8ce808388",
    "oldFingerprint": null,
    "newFingerprint": null
  },
  {
    "bindingId": "2a8638da-8f2d-4b5c-980b-80fa6ce0cf53",
    "nodeKey": "custom:7d843986c524410ab28ee7bed635090f",
    "locator": "managed-files/project-media/files/ca0d945742224b9a9fa2e5356ec3f904/garden-layout-proposal-1-cb4810b1cee84ba28d3b40f95a5690b1.svg",
    "length": 3656,
    "sha256": "c97f22f340ba335cb13ae4f3dbbae15187cbfa5989dbbd124dec6f7f8a168b28",
    "oldFingerprint": "3f67cdd224a63269b89778ece9ab40ca43d98aaec4118be9619af6f18c3ac818",
    "newFingerprint": "772a00ba47b611af36b61cb00acc28390b09536571c6dbab8d975298544518c9"
  },
  {
    "bindingId": "3459c4b0-646a-4265-b416-af05da33d2cb",
    "nodeKey": "custom:350dd9f6d61e43ff9f0270cb66065cf9",
    "locator": "managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72/x-ray-machine-pricing-model-summary-8fb34342b9ba40eb807cbbeffff8baf8.md",
    "length": 1489,
    "sha256": "a41982647fe2c23c169fc8ef134cdb0a73123b4fb1aa1495b2b73952bcdab439",
    "oldFingerprint": null,
    "newFingerprint": null
  },
  {
    "bindingId": "b797e59d-332f-4366-9e4f-224658b967db",
    "nodeKey": "custom:88b9519b9119490c8f75068a2228001f",
    "locator": "managed-files/project-media/files/ca0d945742224b9a9fa2e5356ec3f904/garden-layout-proposal-2-b3b1c2db381a469198651a5d61a09edd.svg",
    "length": 3798,
    "sha256": "f3301eed3a4b50927c79ec300ee5b7cf35ced18d4a0ae18532b7221c4900bd66",
    "oldFingerprint": "7f0336f3df385fd36d67ea59aadab412fc5b9aa075f42c1019fd720ab3f42312",
    "newFingerprint": "2d8abea8a07e204900bad14319cea88561ada14a94138a5725d106278e9568f0"
  },
  {
    "bindingId": "bd34df9e-6968-48d7-ad98-c3350c0c5f82",
    "nodeKey": "custom:6dcbc3c144a64438bf01c78284a191fb",
    "locator": "managed-files/project-media/images/be2ebfd7776643f99b2e8051d0b0d99d/generated-image-0502ef6bcfb84a00bd8fae708fbf7c14.png",
    "length": 2162210,
    "sha256": "695ac3f056bfaa07cda56365d5ae0abacea315221f86141ae0b769e3889d1b93",
    "oldFingerprint": null,
    "newFingerprint": null
  },
  {
    "bindingId": "c8b36596-d184-45b6-8e13-aa41c37e92f3",
    "nodeKey": "custom:52547c00c44044cb8ef6ff44cd01cf12",
    "locator": "managed-files/project-media/files/3324868f66e2478abb8f14f32a5db1e9/office365-category-email-summary-5b869a2cfe3d4866b28c7d950f47c6f6.md",
    "length": 1682,
    "sha256": "7a339b67fcfc54bb2d251ec0760c6ca480c94a176b0145e4bded56f1a4455534",
    "oldFingerprint": "319d549367fecd26d41d0644af0586ef5b520a18ae151e417009f415e579ca6b",
    "newFingerprint": "ee8447e5b074eaad21b07cc53f53db0483c35315e94c2aae04bf0b53e48bcccf"
  },
  {
    "bindingId": "d0be9d36-36eb-404d-a02e-a43b8c006fe1",
    "nodeKey": "custom:1208b5a9dc274d5bb5a8e976371c4049",
    "locator": "managed-files/project-media/images/be2ebfd7776643f99b2e8051d0b0d99d/calculator-repair-completed-e5699f61038c4bbd80396f7b425f45a4.png",
    "length": 143556,
    "sha256": "36dabb4b166ab255a900156fb86c1e33c9fe1e0003017eba927e4c176141e12d",
    "oldFingerprint": "3bf6160eed858acc4b81fd065a84129217539cb0afa68950125c0c914e32c401",
    "newFingerprint": "422e43639b539c28b180434e33d4bc6043926b09b3642996a06b6da189e90a9f"
  },
  {
    "bindingId": "d2b14a6c-b92f-4359-ad74-b82f247a0614",
    "nodeKey": "custom:51a8eba4019047d58b623beb3af0d12c",
    "locator": "managed-files/project-media/files/be2ebfd7776643f99b2e8051d0b0d99d/project-structure-context-brief-8890675cfea448169a893d94451da22a.md",
    "length": 1318,
    "sha256": "859d7c13e5be770a5c31eddcf7fe2a08d0552fd036f7da92a721b1a9c83f21c4",
    "oldFingerprint": null,
    "newFingerprint": null
  },
  {
    "bindingId": "de72765a-1465-40e4-b39e-5367bd9b724a",
    "nodeKey": "custom:2f5d99635ca74a1eba0c673135792ff6",
    "locator": "managed-files/project-media/images/3324868f66e2478abb8f14f32a5db1e9/tetris-faf59b5864254bcdbf10791300e8e07f.png",
    "length": 581806,
    "sha256": "3b9df9398a66b36f89eb2487245b49fc23bbc38d1001abaa7e074561f138300e",
    "oldFingerprint": null,
    "newFingerprint": null
  },
  {
    "bindingId": "f637c7df-cbbf-4325-be66-701b51fae721",
    "nodeKey": "custom:3c9f5cac547f4b2783d8be1ec85830ad",
    "locator": "managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72/x-ray-machine-pricing-model-8124de720ddd4832a7d00f684928b2ca.xlsx",
    "length": 6933,
    "sha256": "fa7bd0abb37c999f9c8bea3d264c36730e353fcd1f989f99d9f02a2fa40caca5",
    "oldFingerprint": null,
    "newFingerprint": null
  }
]
'@ | ConvertFrom-Json

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$launchSettingsPath = Join-Path $repositoryRoot 'src\App\CanDoItAll.Web\Properties\launchSettings.json'
$webOutputPath = Join-Path $repositoryRoot 'src\App\CanDoItAll.Web\bin\Release\net10.0'

function Import-Npgsql {
    if ('Npgsql.NpgsqlConnection' -as [type]) {
        return
    }

    $runtimeCandidates = & dotnet --list-runtimes |
        ForEach-Object {
            if ($_ -match '^Microsoft\.AspNetCore\.App (10\.[^ ]+) \[(.+)\]$') {
                [pscustomobject]@{
                    Version = [Version] $Matches[1]
                    Path = Join-Path $Matches[2] $Matches[1]
                }
            }
        } |
        Sort-Object Version -Descending
    $runtime = $runtimeCandidates | Select-Object -First 1
    if ($null -eq $runtime) {
        throw 'A .NET 10 Microsoft.AspNetCore.App runtime is required.'
    }

    foreach ($assemblyName in @(
        'Microsoft.Extensions.DependencyInjection.Abstractions.dll',
        'Microsoft.Extensions.Logging.Abstractions.dll')) {
        [void] [Reflection.Assembly]::LoadFrom((Join-Path $runtime.Path $assemblyName))
    }

    $npgsqlPath = Join-Path $webOutputPath 'Npgsql.dll'
    if (-not (Test-Path -LiteralPath $npgsqlPath -PathType Leaf)) {
        throw "Build the Release web application before running this repair; '$npgsqlPath' is missing."
    }

    [void] [Reflection.Assembly]::LoadFrom($npgsqlPath)
}

function Add-WindowsIdentityType {
    if ('CanDoItAll.DevRepair.WindowsIdentity' -as [type]) {
        return
    }

    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CanDoItAll.DevRepair
{
    public static class WindowsIdentity
    {
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint FileShareDelete = 0x00000004;
        private const uint OpenExistingDisposition = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint VolumeNameNt = 0x00000002;

        public static string ResolveCanonicalPath(string fullPath)
        {
            var normalizedPath = Path.GetFullPath(fullPath);
            var existingPath = normalizedPath;
            var missingSegments = new Stack<string>();
            while (!File.Exists(existingPath) && !Directory.Exists(existingPath))
            {
                var segment = Path.GetFileName(existingPath);
                if (string.IsNullOrEmpty(segment))
                {
                    throw new InvalidOperationException("No existing filesystem ancestor can prove the managed object identity.");
                }

                missingSegments.Push(segment);
                existingPath = Path.GetDirectoryName(existingPath)
                    ?? throw new InvalidOperationException("No existing filesystem ancestor can prove the managed object identity.");
            }

            var canonicalPath = ResolveExistingCanonicalPath(existingPath);
            while (missingSegments.TryPop(out var segment))
            {
                canonicalPath = Path.Combine(canonicalPath, segment);
            }

            return canonicalPath;
        }

        private static string ResolveExistingCanonicalPath(string existingPath)
        {
            using var handle = OpenExisting(existingPath);
            var capacity = 512;
            while (true)
            {
                var buffer = new char[capacity];
                var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, VolumeNameNt);
                if (length == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, $"Windows could not resolve the final path identity (Win32 error {error}).");
                }

                if (length < buffer.Length)
                {
                    return new string(buffer, 0, checked((int)length));
                }

                capacity = checked((int)length + 1);
            }
        }

        private static SafeFileHandle OpenExisting(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var createFilePath = fullPath.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + fullPath.Substring(2)
                : @"\\?\" + fullPath;
            var handle = CreateFile(
                createFilePath,
                0,
                FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero,
                OpenExistingDisposition,
                FileFlagBackupSemantics,
                IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                return handle;
            }

            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, $"Windows could not open the managed object (Win32 error {error}).");
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file,
            [Out] char[] filePath,
            uint filePathLength,
            uint flags);
    }
}
'@
}

function Get-PhysicalFingerprint([string] $Path) {
    $canonicalPath = [CanDoItAll.DevRepair.WindowsIdentity]::ResolveCanonicalPath($Path).ToUpperInvariant()
    $bytes = [Text.Encoding]::UTF8.GetBytes("filesystem|$canonicalPath")
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-VerifiedHash([string] $Path, [long] $ExpectedLength, [string] $ExpectedHash) {
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSIsContainer -or $item.Length -ne $ExpectedLength) {
        throw "File '$Path' does not have the expected length $ExpectedLength."
    }

    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not [string]::Equals($actualHash, $ExpectedHash, [StringComparison]::Ordinal)) {
        throw "File '$Path' does not have the expected SHA-256."
    }

    return $actualHash
}

function Assert-CanonicalLocator([string] $Locator) {
    if ([IO.Path]::IsPathRooted($Locator) -or
        $Locator.Contains('\') -or
        -not $Locator.StartsWith('managed-files/project-media/', [StringComparison]::Ordinal)) {
        throw "Managed locator '$Locator' is not a canonical project-media relative path."
    }

    $reserved = @('CON', 'PRN', 'AUX', 'NUL', 'COM1', 'COM2', 'COM3', 'COM4', 'COM5', 'COM6', 'COM7', 'COM8', 'COM9', 'LPT1', 'LPT2', 'LPT3', 'LPT4', 'LPT5', 'LPT6', 'LPT7', 'LPT8', 'LPT9')
    foreach ($segment in $Locator.Split('/')) {
        $deviceName = $segment.Split('.', 2)[0].ToUpperInvariant()
        if ([string]::IsNullOrWhiteSpace($segment) -or
            $segment -in @('.', '..') -or
            $segment.EndsWith(' ') -or
            $segment.EndsWith('.') -or
            $segment.Contains(':') -or
            $deviceName -in $reserved) {
            throw "Managed locator '$Locator' contains an unsafe segment."
        }
    }
}

function Resolve-ContainedPath([string] $Root, [string] $Locator) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $fullPath = [IO.Path]::GetFullPath((Join-Path $rootPath $Locator.Replace('/', '\')))
    if (-not $fullPath.StartsWith("$rootPath\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Managed locator '$Locator' escapes '$rootPath'."
    }

    $pathRoot = [IO.Path]::GetPathRoot($fullPath)
    if ([string]::IsNullOrWhiteSpace($pathRoot)) {
        throw "Managed path '$fullPath' does not have a filesystem root."
    }

    $pathsToInspect = [System.Collections.Generic.List[string]]::new()
    $pathsToInspect.Add($pathRoot)
    $cursor = $pathRoot
    foreach ($segment in [IO.Path]::GetRelativePath($pathRoot, $fullPath).Split('\')) {
        $cursor = Join-Path $cursor $segment
        $pathsToInspect.Add($cursor)
    }

    foreach ($pathToInspect in $pathsToInspect) {
        if (Test-Path -LiteralPath $pathToInspect) {
            $item = Get-Item -LiteralPath $pathToInspect -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Managed path '$fullPath' traverses reparse point '$pathToInspect'."
            }
        }
    }

    return $fullPath
}

function Invoke-Scalar($Connection, $Transaction, [string] $Sql) {
    $command = $Connection.CreateCommand()
    try {
        $command.Transaction = $Transaction
        $command.CommandText = $Sql
        return $command.ExecuteScalar()
    }
    finally {
        $command.Dispose()
    }
}

if (-not $IsWindows) {
    throw 'This repair is intentionally limited to the Windows development instance.'
}

$listener = Get-NetTCPConnection -LocalPort 5032 -State Listen -ErrorAction SilentlyContinue
if ($null -ne $listener) {
    throw 'Stop the development web application listening on port 5032 before applying or validating this repair.'
}

Import-Npgsql
Add-WindowsIdentityType

$launchSettings = Get-Content -LiteralPath $launchSettingsPath -Raw | ConvertFrom-Json
$connectionString = $launchSettings.profiles.http.environmentVariables.Database__ConnectionString
$connectionBuilder = [Npgsql.NpgsqlConnectionStringBuilder]::new($connectionString)
if ($connectionBuilder.Host -ne '127.0.0.1' -or
    $connectionBuilder.Port -ne 5432 -or
    $connectionBuilder.Database -ne 'candoitall_development' -or
    $connectionBuilder.Username -ne 'candoitall') {
    throw 'The configured HTTP profile does not target the expected loopback development database.'
}

$manifestById = @{}
foreach ($entry in $manifest) {
    $manifestById.Add(([Guid] $entry.bindingId).ToString('D'), $entry)
}

$connection = [Npgsql.NpgsqlConnection]::new($connectionString)
$transaction = $null
try {
    $connection.Open()
    $transaction = $connection.BeginTransaction([Data.IsolationLevel]::Serializable)
    [void] (Invoke-Scalar $connection $transaction "SELECT pg_advisory_xact_lock(hashtext('candoitall-development-managed-asset-repair'))")

    $catalogCommand = $connection.CreateCommand()
    try {
        $catalogCommand.Transaction = $transaction
        $catalogCommand.CommandText = @'
SELECT "ProviderKind", "IsEnabled", "IsSystemDefault", "IsReadOnly",
       "EndpointOrRoot", "RootBindingFormatVersion", "RootHostBindingId",
       "RootPathState", "RootPathSyntax", "RootPlatformFamily"
FROM "Storage_Catalog"
WHERE "Id" = @storage_id
FOR UPDATE;
'@
        [void] $catalogCommand.Parameters.AddWithValue('storage_id', $storageId)
        $reader = $catalogCommand.ExecuteReader()
        try {
            if (-not $reader.Read()) {
                throw "Storage catalog '$storageId' was not found."
            }

            if ($reader.GetInt32(0) -ne 0 -or
                -not $reader.GetBoolean(1) -or
                -not $reader.GetBoolean(2) -or
                $reader.GetBoolean(3) -or
                -not [string]::Equals($reader.GetString(4), $activeRoot, [StringComparison]::OrdinalIgnoreCase) -or
                $reader.GetInt32(5) -ne 1 -or
                -not [string]::Equals($reader.GetString(6), $expectedHostBindingId, [StringComparison]::Ordinal) -or
                $reader.GetInt32(7) -ne 1 -or
                $reader.GetInt32(8) -ne 2 -or
                $reader.GetInt32(9) -ne 1) {
                throw 'The workspace storage catalog no longer matches the audited development target.'
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $catalogCommand.Dispose()
    }

    $rows = @()
    $bindingCommand = $connection.CreateCommand()
    try {
        $bindingCommand.Transaction = $transaction
        $bindingCommand.CommandText = @'
SELECT b."Id", o."NodeKey", b."MediaRelativePath",
       b."StorageObjectReferenceJson", b."UpdatedAtUtc"
FROM "Workbench_ProjectNodeBindings" b
JOIN "Workbench_ProjectObjects" o ON o."Id" = b."ProjectObjectId"
WHERE b."StorageObjectReferenceJson" LIKE @storage_id_needle
ORDER BY b."Id"
FOR UPDATE OF b;
'@
        [void] $bindingCommand.Parameters.AddWithValue('storage_id_needle', "%$($storageId.ToString('D'))%")
        $reader = $bindingCommand.ExecuteReader()
        try {
            while ($reader.Read()) {
                $rows += [pscustomobject]@{
                    BindingId = $reader.GetGuid(0)
                    NodeKey = $reader.GetString(1)
                    MediaRelativePath = $reader.GetString(2)
                    ReferenceJson = $reader.GetString(3)
                    UpdatedAtUtc = $reader.GetDateTime(4)
                }
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $bindingCommand.Dispose()
    }

    $actualIds = @($rows | ForEach-Object { $_.BindingId.ToString('D') } | Sort-Object)
    $expectedIds = @($manifestById.Keys | Sort-Object)
    if (@(Compare-Object $expectedIds $actualIds).Count -ne 0) {
        throw 'The bindings using the audited storage no longer match the exact 13-row repair manifest.'
    }

    $pendingUpdates = @()
    $copiedCount = 0
    foreach ($row in $rows) {
        $bindingId = $row.BindingId.ToString('D')
        $entry = $manifestById[$bindingId]
        if (-not [string]::Equals($row.NodeKey, $entry.nodeKey, [StringComparison]::Ordinal) -or
            -not [string]::Equals($row.MediaRelativePath, $entry.locator, [StringComparison]::Ordinal)) {
            throw "Binding '$bindingId' no longer matches its audited node and media path."
        }

        Assert-CanonicalLocator $entry.locator
        $sourcePath = Resolve-ContainedPath $sourceRoot $entry.locator
        $destinationPath = Resolve-ContainedPath $activeRoot $entry.locator
        [void] (Get-VerifiedHash $sourcePath ([long] $entry.length) $entry.sha256)

        $reference = $row.ReferenceJson | ConvertFrom-Json -Depth 100
        if (-not [string]::Equals($reference.storageId, $storageId.ToString('D'), [StringComparison]::OrdinalIgnoreCase) -or
            $reference.providerKind -ne 'fileSystem' -or
            $reference.locatorKind -ne 'relativePath' -or
            -not [string]::Equals($reference.locator, $entry.locator, [StringComparison]::Ordinal) -or
            [long] $reference.contentLength -ne [long] $entry.length) {
            throw "Binding '$bindingId' has drifted from its audited storage reference."
        }

        if ($null -ne $entry.oldFingerprint) {
            $provenance = $reference.metadataJson | ConvertFrom-Json -Depth 100
            $assetId = [Guid]::Empty
            if ($provenance.ownershipKind -ne 'project-asset' -or
                [int] $provenance.version -ne 2 -or
                -not [Guid]::TryParse($provenance.assetId, [ref] $assetId) -or
                $assetId -eq [Guid]::Empty -or
                -not [string]::Equals($provenance.requestedManagedPath, $entry.locator, [StringComparison]::Ordinal) -or
                -not [string]::Equals($provenance.storageId, $storageId.ToString('D'), [StringComparison]::OrdinalIgnoreCase) -or
                [int] $provenance.providerKind -ne 0 -or
                [int] $provenance.locatorKind -ne 0 -or
                -not [string]::Equals($provenance.locator, $entry.locator, [StringComparison]::Ordinal) -or
                $null -eq $provenance.originalMetadataJson) {
                throw "Binding '$bindingId' has invalid v2 project-asset provenance."
            }

            $oldFingerprint = Get-PhysicalFingerprint $sourcePath
            $newFingerprint = Get-PhysicalFingerprint $destinationPath
            if ($oldFingerprint -ne $entry.oldFingerprint -or
                $newFingerprint -ne $entry.newFingerprint -or
                $provenance.physicalObjectFingerprint -notin @($entry.oldFingerprint, $entry.newFingerprint)) {
                throw "Binding '$bindingId' failed its physical-identity precondition."
            }

            if ($provenance.physicalObjectFingerprint -ne $entry.newFingerprint) {
                $provenance.physicalObjectFingerprint = $entry.newFingerprint
                $reference.metadataJson = $provenance | ConvertTo-Json -Compress -Depth 100
                $pendingUpdates += [pscustomobject]@{
                    Row = $row
                    NewJson = $reference | ConvertTo-Json -Compress -Depth 100
                }
            }
        }
        else {
            $metadata = $reference.metadataJson | ConvertFrom-Json -Depth 100
            if ($null -ne $metadata.PSObject.Properties['ownershipKind']) {
                throw "Legacy binding '$bindingId' unexpectedly contains managed ownership provenance."
            }
        }

        if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
            [void] (Get-VerifiedHash $destinationPath ([long] $entry.length) $entry.sha256)
        }
        elseif ($Apply) {
            $destinationDirectory = Split-Path -Parent $destinationPath
            [void] [IO.Directory]::CreateDirectory($destinationDirectory)
            [void] (Resolve-ContainedPath $activeRoot $entry.locator)
            $stagingPath = Join-Path $destinationDirectory ".$(Split-Path -Leaf $destinationPath).repair-$([Guid]::NewGuid().ToString('N')).tmp"
            $stagingOwned = $false
            try {
                $sourceStream = [IO.File]::Open($sourcePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
                try {
                    $targetStream = [IO.File]::Open($stagingPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                    $stagingOwned = $true
                    try {
                        $sourceStream.CopyTo($targetStream)
                        $targetStream.Flush($true)
                    }
                    finally {
                        $targetStream.Dispose()
                    }
                }
                finally {
                    $sourceStream.Dispose()
                }

                [void] (Get-VerifiedHash $stagingPath ([long] $entry.length) $entry.sha256)
                [IO.File]::Move($stagingPath, $destinationPath, $false)
                $copiedCount++
            }
            finally {
                if ($stagingOwned -and (Test-Path -LiteralPath $stagingPath -PathType Leaf)) {
                    Remove-Item -LiteralPath $stagingPath -Force
                }
            }

            [void] (Get-VerifiedHash $sourcePath ([long] $entry.length) $entry.sha256)
            [void] (Get-VerifiedHash $destinationPath ([long] $entry.length) $entry.sha256)
        }
    }

    if ($Apply) {
        foreach ($row in $rows) {
            $entry = $manifestById[$row.BindingId.ToString('D')]
            $sourcePath = Resolve-ContainedPath $sourceRoot $entry.locator
            $destinationPath = Resolve-ContainedPath $activeRoot $entry.locator
            [void] (Get-VerifiedHash $sourcePath ([long] $entry.length) $entry.sha256)
            [void] (Get-VerifiedHash $destinationPath ([long] $entry.length) $entry.sha256)
            if ($null -ne $entry.newFingerprint -and
                (Get-PhysicalFingerprint $destinationPath) -ne $entry.newFingerprint) {
                throw "Binding '$($row.BindingId)' destination identity changed before commit."
            }
        }

        foreach ($update in $pendingUpdates) {
            $command = $connection.CreateCommand()
            try {
                $command.Transaction = $transaction
                $command.CommandText = @'
UPDATE "Workbench_ProjectNodeBindings"
SET "StorageObjectReferenceJson" = @new_json,
    "UpdatedAtUtc" = @updated_at_utc
WHERE "Id" = @id
  AND "StorageObjectReferenceJson" = @expected_json
  AND "UpdatedAtUtc" = @expected_updated_at_utc;
'@
                [void] $command.Parameters.AddWithValue('new_json', $update.NewJson)
                [void] $command.Parameters.AddWithValue('updated_at_utc', [DateTime]::UtcNow)
                [void] $command.Parameters.AddWithValue('id', $update.Row.BindingId)
                [void] $command.Parameters.AddWithValue('expected_json', $update.Row.ReferenceJson)
                [void] $command.Parameters.AddWithValue('expected_updated_at_utc', $update.Row.UpdatedAtUtc)
                if ($command.ExecuteNonQuery() -ne 1) {
                    throw "Binding '$($update.Row.BindingId)' changed while the repair was running."
                }
            }
            finally {
                $command.Dispose()
            }
        }

        $transaction.Commit()
        Write-Host "Applied managed-asset repair: copied $copiedCount file(s), restamped $($pendingUpdates.Count) v2 binding(s), preserved all 13 historical source files."
    }
    else {
        $transaction.Rollback()
        Write-Host "Dry run passed for all 13 bindings. Apply would copy missing files and restamp $($pendingUpdates.Count) v2 binding(s); no data was changed."
    }
}
catch {
    if ($null -ne $transaction -and $null -ne $transaction.Connection) {
        $transaction.Rollback()
    }

    throw
}
finally {
    if ($null -ne $transaction) {
        $transaction.Dispose()
    }

    $connection.Dispose()
}
