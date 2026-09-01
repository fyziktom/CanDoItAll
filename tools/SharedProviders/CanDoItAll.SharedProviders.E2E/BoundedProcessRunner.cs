#nullable enable

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.SharedProviders.E2E;

public sealed record BoundedProcessResult(
    int ExitCode,
    bool TimedOut,
    bool OutputLimitExceeded,
    long StandardOutputBytes,
    long StandardErrorBytes,
    string StandardOutput,
    string StandardError);

public static class BoundedProcessRunner
{
    private static readonly TimeSpan PostKillGrace = TimeSpan.FromSeconds(3);

    public static BoundedProcessResult Run(
        string fileName,
        string[] arguments,
        string workingDirectory,
        string? standardOutputPath,
        string? standardErrorPath,
        int timeoutSeconds,
        long maximumStandardOutputBytes,
        long maximumStandardErrorBytes,
        string[] environmentKeysToRemove,
        string[] environmentAssignments)
        => RunAsync(
                fileName,
                arguments,
                workingDirectory,
                standardOutputPath,
                standardErrorPath,
                timeoutSeconds,
                maximumStandardOutputBytes,
                maximumStandardErrorBytes,
                environmentKeysToRemove,
                environmentAssignments)
            .GetAwaiter()
            .GetResult();

    private static async Task<BoundedProcessResult> RunAsync(
        string fileName,
        string[] arguments,
        string workingDirectory,
        string? standardOutputPath,
        string? standardErrorPath,
        int timeoutSeconds,
        long maximumStandardOutputBytes,
        long maximumStandardErrorBytes,
        string[] environmentKeysToRemove,
        string[] environmentAssignments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environmentKeysToRemove);
        ArgumentNullException.ThrowIfNull(environmentAssignments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (timeoutSeconds <= 0 ||
            maximumStandardOutputBytes <= 0 ||
            maximumStandardErrorBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutSeconds),
                "Process timeout and output limits must be positive.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var removalKeys = environmentKeysToRemove.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in startInfo.Environment.Keys
                     .Where(key => key.StartsWith("E2E_", StringComparison.OrdinalIgnoreCase) ||
                         removalKeys.Contains(key))
                     .ToArray())
        {
            startInfo.Environment.Remove(key);
        }

        foreach (var assignment in environmentAssignments)
        {
            var separatorIndex = assignment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                throw new ArgumentException(
                    "A child-process environment assignment is invalid.",
                    nameof(environmentAssignments));
            }

            startInfo.Environment[assignment[..separatorIndex]] = assignment[(separatorIndex + 1)..];
        }

        using var process = new Process { StartInfo = startInfo };
        using var standardOutput = CreateDestination(standardOutputPath);
        using var standardError = CreateDestination(standardErrorPath);
        if (!process.Start())
        {
            throw new InvalidOperationException("The bounded process could not be started.");
        }

        using var pumpCancellation = new CancellationTokenSource();
        var outputPump = new ProcessOutputPump(
            process.StandardOutput.BaseStream,
            standardOutput.Stream,
            maximumStandardOutputBytes);
        var errorPump = new ProcessOutputPump(
            process.StandardError.BaseStream,
            standardError.Stream,
            maximumStandardErrorBytes);
        var outputTask = outputPump.RunAsync(pumpCancellation.Token);
        var errorTask = errorPump.RunAsync(pumpCancellation.Token);
        var waitTask = process.WaitForExitAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var timedOut = false;
        var outputLimitExceeded = false;
        Exception? unexpectedPumpFailure = null;

        while (!waitTask.IsCompleted || !outputTask.IsCompleted || !errorTask.IsCompleted)
        {
            if (timeoutTask.IsCompleted)
            {
                timedOut = true;
                break;
            }

            CapturePumpFailures(
                outputTask,
                errorTask,
                ref outputLimitExceeded,
                ref unexpectedPumpFailure);
            if (outputLimitExceeded || unexpectedPumpFailure is not null)
            {
                break;
            }

            var pending = new List<Task> { timeoutTask };
            if (!waitTask.IsCompleted)
            {
                pending.Add(waitTask);
            }

            if (!outputTask.IsCompleted)
            {
                pending.Add(outputTask);
            }

            if (!errorTask.IsCompleted)
            {
                pending.Add(errorTask);
            }

            await Task.WhenAny(pending);
        }

        CapturePumpFailures(
            outputTask,
            errorTask,
            ref outputLimitExceeded,
            ref unexpectedPumpFailure);
        if (timedOut || outputLimitExceeded || unexpectedPumpFailure is not null)
        {
            pumpCancellation.Cancel();
            TryKill(process);
            var completedWithinGrace = await WaitForCompletionWithinGraceAsync(
                waitTask,
                outputTask,
                errorTask);
            if (!completedWithinGrace)
            {
                TryCloseRedirectedStreams(process);
                ObservePendingFaults(waitTask, outputTask, errorTask);
            }
        }

        if (unexpectedPumpFailure is not null)
        {
            throw new InvalidOperationException("A bounded process output stream failed.");
        }

        return new BoundedProcessResult(
            GetExitCode(process),
            timedOut,
            outputLimitExceeded,
            outputPump.BytesWritten,
            errorPump.BytesWritten,
            outputTask.IsCompleted ? standardOutput.ReadText() : string.Empty,
            errorTask.IsCompleted ? standardError.ReadText() : string.Empty);
    }

    private static void CapturePumpFailures(
        Task outputTask,
        Task errorTask,
        ref bool outputLimitExceeded,
        ref Exception? unexpectedPumpFailure)
    {
        foreach (var task in new[] { outputTask, errorTask })
        {
            if (!task.IsFaulted)
            {
                continue;
            }

            var failures = task.Exception?.Flatten().InnerExceptions ?? [];
            outputLimitExceeded |= failures.Any(exception => exception is ProcessOutputLimitException);
            unexpectedPumpFailure ??= failures.FirstOrDefault(
                exception => exception is not ProcessOutputLimitException);
        }
    }

    private static async Task<bool> WaitForCompletionWithinGraceAsync(params Task[] tasks)
    {
        var pendingTasks = tasks.Where(task => !task.IsCompleted).ToArray();
        if (pendingTasks.Length == 0)
        {
            return true;
        }

        var completion = Task.WhenAll(pendingTasks.Select(ObserveCompletionAsync));
        var deadline = Task.Delay(PostKillGrace);
        return ReferenceEquals(await Task.WhenAny(completion, deadline), completion);
    }

    private static async Task ObserveCompletionAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    private static void ObservePendingFaults(params Task[] tasks)
    {
        foreach (var task in tasks.Where(task => !task.IsCompleted))
        {
            _ = task.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private static ProcessOutputDestination CreateDestination(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ProcessOutputDestination(new MemoryStream(), captureText: true);
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.ReadWrite,
            Share = FileShare.Read,
            BufferSize = 16 * 1024,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new ProcessOutputDestination(new FileStream(fullPath, options), captureText: false);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            NotSupportedException or
            Win32Exception)
        {
        }
    }

    private static void TryCloseRedirectedStreams(Process process)
    {
        try
        {
            process.StandardOutput.Dispose();
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            IOException or
            ObjectDisposedException)
        {
        }

        try
        {
            process.StandardError.Dispose();
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            IOException or
            ObjectDisposedException)
        {
        }
    }

    private static int GetExitCode(Process process)
    {
        try
        {
            return process.HasExited ? process.ExitCode : -1;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private sealed class ProcessOutputPump(
        Stream source,
        Stream destination,
        long maximumBytes)
    {
        private long bytesWritten;

        public long BytesWritten => Interlocked.Read(ref bytesWritten);

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            long totalBytes = 0;
            while (true)
            {
                var bytesRead = await source.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    await destination.FlushAsync(cancellationToken);
                    return;
                }

                if (totalBytes > maximumBytes - bytesRead)
                {
                    throw new ProcessOutputLimitException();
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytes += bytesRead;
                Interlocked.Exchange(ref bytesWritten, totalBytes);
            }
        }
    }

    private sealed class ProcessOutputDestination(Stream stream, bool captureText) : IDisposable
    {
        public Stream Stream { get; } = stream;

        public string ReadText()
        {
            if (!captureText)
            {
                return string.Empty;
            }

            Stream.Position = 0;
            using var reader = new StreamReader(
                Stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);
            return reader.ReadToEnd();
        }

        public void Dispose() => Stream.Dispose();
    }

    private sealed class ProcessOutputLimitException : IOException;
}
