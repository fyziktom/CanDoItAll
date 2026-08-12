using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace CanDoItAll.AgentFramework.Core;

internal interface ILocalWorkspaceProcessOwnership : IDisposable
{
    WorkspaceOwnedProcessBoundary Identity { get; }

    bool RequestGracefulTermination(Process rootProcess);

    bool ForceTerminate();

    Task<bool> WaitForEmptyAsync(TimeSpan timeout);
}

internal abstract class LocalWorkspaceProcessOwnershipStart : IDisposable
{
    public abstract WorkspaceOwnedProcessBoundary Identity { get; }

    public abstract ILocalWorkspaceProcessOwnership Attach(Process process);

    public abstract void Dispose();

    public static LocalWorkspaceProcessOwnershipStart Prepare(
        ProcessStartInfo startInfo,
        string executablePath,
        string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsJobObjectOwnershipStart.PrepareStart(startInfo);
        }

        return UnixProcessGroupOwnershipStart.PrepareStart(
            startInfo,
            executablePath,
            workingDirectory);
    }

    public static ILocalWorkspaceProcessOwnership? TryOpen(
        WorkspaceOwnedProcessBoundary identity)
        => identity.Kind switch
        {
            WorkspaceOwnedProcessBoundaryKind.WindowsJobObject when OperatingSystem.IsWindows() =>
                WindowsJobObjectOwnership.TryOpen(identity),
            WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup when !OperatingSystem.IsWindows() =>
                new UnixProcessGroupOwnership(identity),
            _ => null
        };
}

internal sealed class WindowsJobObjectOwnershipStart : LocalWorkspaceProcessOwnershipStart
{
    private SafeJobHandle? jobHandle;

    private WindowsJobObjectOwnershipStart(
        SafeJobHandle jobHandle,
        WorkspaceOwnedProcessBoundary identity)
    {
        this.jobHandle = jobHandle;
        Identity = identity;
    }

    public override WorkspaceOwnedProcessBoundary Identity { get; }

    [SupportedOSPlatform("windows")]
    public static WindowsJobObjectOwnershipStart PrepareStart(ProcessStartInfo startInfo)
    {
        var instanceId = Guid.NewGuid();
        var identity = new WorkspaceOwnedProcessBoundary(
            WorkspaceOwnedProcessBoundaryKind.WindowsJobObject,
            NativeId: 0,
            instanceId);
        var handle = WindowsJobObjectNativeMethods.CreateJobObject(
            0,
            WindowsJobObjectOwnership.GetJobName(instanceId));
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not create the owned process Job Object.");
        }

        try
        {
            var limits = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = JobObjectLimitFlags.KillOnJobClose
                }
            };
            if (WindowsJobObjectNativeMethods.SetInformationJobObject(
                    handle,
                    JobObjectInformationClass.ExtendedLimitInformation,
                    ref limits,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not configure the owned process Job Object.");
            }

            startInfo.CreateNewProcessGroup = true;
            return new WindowsJobObjectOwnershipStart(handle, identity);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public override ILocalWorkspaceProcessOwnership Attach(Process process)
    {
        var handle = jobHandle
            ?? throw new ObjectDisposedException(nameof(WindowsJobObjectOwnershipStart));
        if (WindowsJobObjectNativeMethods.AssignProcessToJobObject(handle, process.SafeHandle) == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not assign the process to its owned Job Object.");
        }

        jobHandle = null;
        return new WindowsJobObjectOwnership(handle, Identity);
    }

    public override void Dispose()
    {
        jobHandle?.Dispose();
        jobHandle = null;
    }
}

internal sealed class WindowsJobObjectOwnership : ILocalWorkspaceProcessOwnership
{
    private const uint JobObjectQuery = 0x0004;
    private const uint JobObjectTerminate = 0x0008;
    private SafeJobHandle? jobHandle;

    public WindowsJobObjectOwnership(
        SafeJobHandle jobHandle,
        WorkspaceOwnedProcessBoundary identity)
    {
        this.jobHandle = jobHandle;
        Identity = identity;
    }

    public WorkspaceOwnedProcessBoundary Identity { get; }

    public bool RequestGracefulTermination(Process rootProcess)
    {
        try
        {
            return rootProcess.HasExited || rootProcess.CloseMainWindow();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            return false;
        }
    }

    public bool ForceTerminate()
    {
        var handle = jobHandle;
        if (handle is null || handle.IsInvalid)
        {
            return false;
        }

        if (IsEmpty(handle))
        {
            return true;
        }

        return WindowsJobObjectNativeMethods.TerminateJobObject(handle, 1) != 0;
    }

    public async Task<bool> WaitForEmptyAsync(TimeSpan timeout)
    {
        var handle = jobHandle;
        if (handle is null || handle.IsInvalid)
        {
            return false;
        }

        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (IsEmpty(handle))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }

        return IsEmpty(handle);
    }

    public void Dispose()
    {
        jobHandle?.Dispose();
        jobHandle = null;
    }

    public static WindowsJobObjectOwnership? TryOpen(WorkspaceOwnedProcessBoundary identity)
    {
        if (identity.InstanceId == Guid.Empty)
        {
            return null;
        }

        var handle = WindowsJobObjectNativeMethods.OpenJobObject(
            JobObjectQuery | JobObjectTerminate,
            inheritHandle: 0,
            GetJobName(identity.InstanceId));
        return handle.IsInvalid
            ? null
            : new WindowsJobObjectOwnership(handle, identity);
    }

    public static string GetJobName(Guid instanceId)
        => $"Local\\CanDoItAll.Workspace.{instanceId:N}";

    private static bool IsEmpty(SafeJobHandle handle)
    {
        if (WindowsJobObjectNativeMethods.QueryInformationJobObject(
                handle,
                JobObjectInformationClass.BasicAccountingInformation,
                out var accounting,
                (uint)Marshal.SizeOf<JobObjectBasicAccountingInformation>(),
                out _) == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not query the owned process Job Object.");
        }

        return accounting.ActiveProcesses == 0;
    }
}

internal sealed class UnixProcessGroupOwnershipStart : LocalWorkspaceProcessOwnershipStart
{
    private const string PerlSessionBootstrap =
        "my $program = shift @ARGV; POSIX::setsid() >= 0 or die \"setsid: $!\"; kill 'STOP', $$ or die \"stop: $!\"; exec {$program} $program, @ARGV or die \"exec: $!\";";
    private const string ShellSessionBootstrap =
        "kill -STOP $$; exec \"$@\"";

    private UnixProcessGroupOwnershipStart()
    {
    }

    public override WorkspaceOwnedProcessBoundary Identity
        => throw new InvalidOperationException("The Unix process group identity is assigned after process start.");

    public static UnixProcessGroupOwnershipStart PrepareStart(
        ProcessStartInfo startInfo,
        string executablePath,
        string workingDirectory)
    {
        var arguments = startInfo.ArgumentList.ToArray();
        startInfo.ArgumentList.Clear();
        if (OperatingSystem.IsMacOS())
        {
            startInfo.FileName = new WorkspaceExecutableLocator().ResolveExecutablePath(
                ["/usr/bin/perl", "perl"],
                workingDirectory);
            startInfo.ArgumentList.Add("-MPOSIX");
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(PerlSessionBootstrap);
            startInfo.ArgumentList.Add("--");
        }
        else
        {
            startInfo.FileName = new WorkspaceExecutableLocator().ResolveExecutablePath(
                ["/usr/bin/setsid", "setsid"],
                workingDirectory);
            startInfo.ArgumentList.Add("/bin/sh");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(ShellSessionBootstrap);
            startInfo.ArgumentList.Add("candoitall-session-bootstrap");
        }

        startInfo.ArgumentList.Add(executablePath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new UnixProcessGroupOwnershipStart();
    }

    public override ILocalWorkspaceProcessOwnership Attach(Process process)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var processGroupId = UnixProcessNativeMethods.GetProcessGroupId(process.Id);
            if (processGroupId == process.Id)
            {
                var identity = new WorkspaceOwnedProcessBoundary(
                    WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup,
                    processGroupId,
                    Guid.Empty);
                var continueSignal = OperatingSystem.IsMacOS() ? 19 : 18;
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    if (UnixProcessNativeMethods.Kill(process.Id, continueSignal) != 0)
                    {
                        if (LocalWorkspaceProcessHost.ProcessHasExited(process))
                        {
                            break;
                        }

                        throw new Win32Exception(
                            Marshal.GetLastPInvokeError(),
                            "The owned Unix process group could not be resumed after attachment.");
                    }

                    Thread.Sleep(5);
                }

                return new UnixProcessGroupOwnership(identity);
            }

            if (LocalWorkspaceProcessHost.ProcessHasExited(process))
            {
                break;
            }

            Thread.Sleep(5);
        }

        throw new InvalidOperationException("The process did not enter its dedicated Unix process group.");
    }

    public override void Dispose()
    {
    }
}

internal sealed class UnixProcessGroupOwnership : ILocalWorkspaceProcessOwnership
{
    private const int SignalExists = 0;
    private const int SignalTerminate = 15;
    private const int SignalKill = 9;
    private const int NoSuchProcess = 3;

    public UnixProcessGroupOwnership(WorkspaceOwnedProcessBoundary identity)
    {
        if (identity.NativeId <= 0 || identity.NativeId > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(identity), "The Unix process group identity is invalid.");
        }

        Identity = identity;
    }

    public WorkspaceOwnedProcessBoundary Identity { get; }

    public bool RequestGracefulTermination(Process rootProcess)
        => SignalGroup(SignalTerminate);

    public bool ForceTerminate()
        => SignalGroup(SignalKill);

    public async Task<bool> WaitForEmptyAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (IsEmpty())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }

        return IsEmpty();
    }

    public void Dispose()
    {
    }

    private bool SignalGroup(int signal)
    {
        if (UnixProcessNativeMethods.Kill(checked(-(int)Identity.NativeId), signal) == 0)
        {
            return true;
        }

        return Marshal.GetLastPInvokeError() == NoSuchProcess;
    }

    private bool IsEmpty()
    {
        if (UnixProcessNativeMethods.Kill(checked(-(int)Identity.NativeId), SignalExists) == 0)
        {
            return false;
        }

        return Marshal.GetLastPInvokeError() == NoSuchProcess;
    }
}

internal sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeJobHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
        => WindowsJobObjectNativeMethods.CloseHandle(handle) != 0;
}

[Flags]
internal enum JobObjectLimitFlags : uint
{
    KillOnJobClose = 0x00002000
}

internal enum JobObjectInformationClass
{
    BasicAccountingInformation = 1,
    ExtendedLimitInformation = 9
}

[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectBasicAccountingInformation
{
    public long TotalUserTime;
    public long TotalKernelTime;
    public long ThisPeriodTotalUserTime;
    public long ThisPeriodTotalKernelTime;
    public uint TotalPageFaultCount;
    public uint TotalProcesses;
    public uint ActiveProcesses;
    public uint TotalTerminatedProcesses;
}

[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectBasicLimitInformation
{
    public long PerProcessUserTimeLimit;
    public long PerJobUserTimeLimit;
    public JobObjectLimitFlags LimitFlags;
    public nuint MinimumWorkingSetSize;
    public nuint MaximumWorkingSetSize;
    public uint ActiveProcessLimit;
    public nuint Affinity;
    public uint PriorityClass;
    public uint SchedulingClass;
}

[StructLayout(LayoutKind.Sequential)]
internal struct IoCounters
{
    public ulong ReadOperationCount;
    public ulong WriteOperationCount;
    public ulong OtherOperationCount;
    public ulong ReadTransferCount;
    public ulong WriteTransferCount;
    public ulong OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct JobObjectExtendedLimitInformation
{
    public JobObjectBasicLimitInformation BasicLimitInformation;
    public IoCounters IoInfo;
    public nuint ProcessMemoryLimit;
    public nuint JobMemoryLimit;
    public nuint PeakProcessMemoryUsed;
    public nuint PeakJobMemoryUsed;
}

internal static partial class WindowsJobObjectNativeMethods
{
    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial SafeJobHandle CreateJobObject(nint jobAttributes, string? name);

    [LibraryImport("kernel32.dll", EntryPoint = "OpenJobObjectW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial SafeJobHandle OpenJobObject(uint desiredAccess, int inheritHandle, string name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int SetInformationJobObject(
        SafeJobHandle job,
        JobObjectInformationClass informationClass,
        ref JobObjectExtendedLimitInformation information,
        uint informationLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int AssignProcessToJobObject(SafeJobHandle job, SafeProcessHandle process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int TerminateJobObject(SafeJobHandle job, uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int QueryInformationJobObject(
        SafeJobHandle job,
        JobObjectInformationClass informationClass,
        out JobObjectBasicAccountingInformation information,
        uint informationLength,
        out uint returnLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int CloseHandle(nint handle);
}

internal static partial class UnixProcessNativeMethods
{
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    internal static partial int Kill(int processId, int signal);

    [LibraryImport("libc", EntryPoint = "getpgid", SetLastError = true)]
    internal static partial int GetProcessGroupId(int processId);

    [LibraryImport("libc", EntryPoint = "realpath", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial nint RealPath(string path, nint resolvedPath);

    [LibraryImport("libc", EntryPoint = "free")]
    internal static partial void Free(nint pointer);
}
