param(
    [Parameter(Mandatory)][int]$AppPid,
    [Parameter(Mandatory)][int]$ParentPid,
    [Parameter(Mandatory)][string]$ExpectedAppStartUtc,
    [Parameter(Mandatory)][string]$OutputPath,
    [int]$WrapperPid = 0,
    [switch]$Signal
)
$ErrorActionPreference = 'Stop'
$process = Get-Process -Id $AppPid -ErrorAction Stop
if ($process.StartTime.ToUniversalTime().ToString('O') -ne $ExpectedAppStartUtc) {
    throw 'The native app PID/start identity changed.'
}
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
public static class StartupConsoleController {
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetConsoleProcessList([Out] uint[] processList, uint processCount);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint controlEvent, uint processGroupId);
    public static object InspectOrSignal(uint appId, uint parentId, uint wrapperId, bool signal) {
        FreeConsole();
        if (!AttachConsole(appId)) {
            throw new InvalidOperationException("Cannot attach to the exact application console; error " + Marshal.GetLastWin32Error());
        }
        try {
            var processIds = new uint[64];
            var count = GetConsoleProcessList(processIds, (uint)processIds.Length);
            if (count == 0 || count > processIds.Length) {
                throw new InvalidOperationException("Cannot bound the target console process list.");
            }
            Array.Resize(ref processIds, (int)count);
            var expected = new HashSet<uint> { appId, parentId, (uint)Environment.ProcessId };
            if (wrapperId != 0) {
                expected.Add(wrapperId);
            }
            if (!expected.SetEquals(processIds)) {
                throw new InvalidOperationException("The application console contains unexpected or missing processes. No signal was sent.");
            }
            if (signal) {
                if (!SetConsoleCtrlHandler(IntPtr.Zero, true)) {
                    throw new InvalidOperationException("Cannot isolate the signal helper from CTRL_C.");
                }
                if (!GenerateConsoleCtrlEvent(0, 0)) {
                    throw new InvalidOperationException("The bounded CTRL_C signal failed; error " + Marshal.GetLastWin32Error());
                }
            }
            return new { ProcessIds = processIds.OrderBy(value => value).ToArray(), SignalSent = signal };
        } finally {
            FreeConsole();
        }
    }
}
'@
[StartupConsoleController]::InspectOrSignal([uint32]$AppPid, [uint32]$ParentPid, [uint32]$WrapperPid, [bool]$Signal) |
    ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
