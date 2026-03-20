using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MusicNotation.Editor.Services;
using MusicTheory.Core.Models;
using MusicTheory.Core.Recognition;

namespace App.Blazor.Services;

public sealed class RealtimeChordDetectionSessionService(
    IMidiService midiService,
    RealtimeChordDetectionService detectionService,
    ILogger<RealtimeChordDetectionSessionService> logger)
    : IAsyncDisposable
{
    private readonly IMidiService midiService = midiService;
    private readonly RealtimeChordDetectionService detectionService = detectionService;
    private readonly ILogger<RealtimeChordDetectionSessionService> logger = logger;
    private readonly RealtimeChordWindowDetector detector = new(new RealtimeChordWindowOptions());
    private readonly RealtimeNoteScoreTracker scoreTracker = new(new RealtimeNoteScoreOptions());
    private readonly SemaphoreSlim gate = new(1, 1);

    private RealtimeChordDetectionResult currentResult = new(
        Snapshot: new RealtimeChordWindowSnapshot(
            ActiveNotes: Array.Empty<RealtimeActiveNoteInfo>(),
            ActivePitchClasses: Array.Empty<int>(),
            DuplicatePitchClassCounts: new Dictionary<int, int>(),
            BassPitchClass: null,
            SustainPedalDown: false,
            IsStable: false,
            TooManyNotes: false,
            WindowSpanMs: 0,
            TimestampMs: 0),
        Candidates: Array.Empty<RealtimeChordCandidate>(),
        HasDenseClusterWarning: false,
        WarningMessage: null,
        InferredScaleContext: Array.Empty<CandidateScaleSuggestion>());
    private int evaluationVersion;
    private bool initialized;
    private bool disposed;
    private RealtimeNoteScoreSnapshot currentScoreSnapshot = RealtimeNoteScoreSnapshot.Empty();
    private string? lastDebugSignature;
    private double lastDebugTimestampMs;

    public event Action<RealtimeChordDetectionResult>? DetectionChanged;

    public RealtimeChordDetectionResult CurrentResult => currentResult;
    public RealtimeNoteScoreSnapshot CurrentScoreSnapshot => currentScoreSnapshot;
    public bool IsInitialized => initialized;
    public bool IsSupported => midiService.IsSupported;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return;
            }

            try
            {
                await midiService.InitializeAsync().ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                // Browser can block Web MIDI; detector still supports manual fallback.
            }

            midiService.MessageReceived += HandleMidiMessageReceived;
            initialized = true;
            await EvaluateNowAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SetManualNotesAsync(
        IReadOnlyCollection<int> midiNotes,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            detector.Reset();
            scoreTracker.Reset();
            var timestampMs = GetTimestampMs();
            foreach (var note in midiNotes.Distinct())
            {
                var midiEvent = RealtimeMidiEvent.NoteOn(note, 96, timestampMs);
                detector.Apply(midiEvent);
                scoreTracker.Apply(midiEvent);
            }
        }
        finally
        {
            gate.Release();
        }

        await QueueEvaluationAsync(cancellationToken).ConfigureAwait(false);
    }

    private void HandleMidiMessageReceived(object? _, ParsedMidiMessage message)
    {
        if (disposed || !TryMapMessage(message, out var midiEvent))
        {
            return;
        }

        detector.Apply(midiEvent);
        scoreTracker.Apply(midiEvent);
        _ = QueueEvaluationAsync(CancellationToken.None);
    }

    private async Task QueueEvaluationAsync(CancellationToken cancellationToken)
    {
        var version = Interlocked.Increment(ref evaluationVersion);
        try
        {
            await Task.Delay(detector.Options.DebounceMs, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != evaluationVersion || disposed)
        {
            return;
        }

        await EvaluateNowAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task EvaluateNowAsync(CancellationToken cancellationToken)
    {
        var nowMs = GetTimestampMs();
        var evalStart = Stopwatch.GetTimestamp();
        var snapshot = detector.GetSnapshot(nowMs);
        var scoreSnapshot = scoreTracker.GetSnapshot(nowMs);
        currentScoreSnapshot = scoreSnapshot;
        var result = detectionService.Detect(
            snapshot,
            scoreSnapshot,
            new RealtimeChordDetectionOptions(
                Difficulty: DifficultyPreset.Intermediate,
                EnharmonicPreference: EnharmonicPreference.Sharps,
                MaxCandidates: 8));
        currentResult = result;
        var elapsedMs = (Stopwatch.GetTimestamp() - evalStart) * 1000d / Stopwatch.Frequency;
        LogDetectionDebug(result, scoreSnapshot, elapsedMs, nowMs);
        DetectionChanged?.Invoke(result);
        return Task.CompletedTask;
    }

    private void LogDetectionDebug(
        RealtimeChordDetectionResult result,
        RealtimeNoteScoreSnapshot scoreSnapshot,
        double elapsedMs,
        double nowMs)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var topCandidate = result.Candidates.FirstOrDefault();
        var topScale = result.InferredScaleContext.FirstOrDefault();
        var signature = $"{topCandidate?.DisplayName}|{topScale?.Name}|{scoreSnapshot.BassPitchClass}|{result.Snapshot.ActiveNotes.Count}";
        if (signature == lastDebugSignature && (nowMs - lastDebugTimestampMs) < 2000)
        {
            return;
        }

        lastDebugSignature = signature;
        lastDebugTimestampMs = nowMs;
        logger.LogDebug(
            "Chord detection eval {LatencyMs:0.0}ms | Top chord {Chord} score {ChordScore:0.00} | Top inferred scale {Scale} score {ScaleScore:0.00} | Pitch classes {PitchClassCount} | Stable {IsStable}",
            elapsedMs,
            topCandidate?.DisplayName ?? "(none)",
            topCandidate?.Score ?? 0,
            topScale?.Name ?? "(none)",
            topScale?.Confidence ?? 0,
            scoreSnapshot.RankedPitchClassScores.Count,
            result.Snapshot.IsStable);
    }

    private static bool TryMapMessage(ParsedMidiMessage message, out RealtimeMidiEvent midiEvent)
    {
        var timestampMs = message.Timestamp > 0 ? message.Timestamp : GetTimestampMs();
        switch (message.Type)
        {
            case MidiMessageType.NoteOn when message.Note.HasValue:
                if ((message.Velocity ?? 0) <= 0)
                {
                    midiEvent = RealtimeMidiEvent.NoteOff(message.Note.Value, 0, timestampMs);
                }
                else
                {
                    midiEvent = RealtimeMidiEvent.NoteOn(message.Note.Value, message.Velocity ?? 96, timestampMs);
                }

                return true;
            case MidiMessageType.NoteOff when message.Note.HasValue:
                midiEvent = RealtimeMidiEvent.NoteOff(message.Note.Value, message.Velocity ?? 0, timestampMs);
                return true;
            case MidiMessageType.ControlChange when message.Controller == 64:
                midiEvent = RealtimeMidiEvent.Sustain((message.Value ?? 0) >= 64, timestampMs);
                return true;
            default:
                midiEvent = default;
                return false;
        }
    }

    private static double GetTimestampMs()
    {
        return Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;
    }

    public ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        midiService.MessageReceived -= HandleMidiMessageReceived;
        gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
