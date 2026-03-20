using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MusicTheory.Core.Generation.Realtime;
using MusicTheory.Core.Models;
using MusicTheory.Core.Recognition;
using MusicTheory.Core.Theory;

namespace App.Blazor.Services;

public sealed class HarmonicAssistantSessionService(
    RealtimeChordDetectionSessionService chordDetectionSession,
    RealtimeHarmonicAssistantEngine assistantEngine,
    ILogger<HarmonicAssistantSessionService> logger)
    : IDisposable
{
    private readonly RealtimeChordDetectionSessionService chordDetectionSession = chordDetectionSession;
    private readonly RealtimeHarmonicAssistantEngine assistantEngine = assistantEngine;
    private readonly ILogger<HarmonicAssistantSessionService> logger = logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int updateVersion;
    private string? lastDebugSignature;
    private double lastDebugTimestampMs;

    private HarmonicAssistantUpdate currentUpdate = new(HarmonicAssistantState.Default, Array.Empty<HarmonicSuggestionPath>());

    public event Action<HarmonicAssistantUpdate>? UpdateChanged;

    public HarmonicAssistantUpdate CurrentUpdate => currentUpdate;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        chordDetectionSession.DetectionChanged += HandleChordDetectionChanged;
        await chordDetectionSession.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ConfigureAsync(AssistantSettings settings, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updateStart = Stopwatch.GetTimestamp();
            currentUpdate = assistantEngine.Update(chordDetectionSession.CurrentResult, settings, DateTimeOffset.UtcNow);
            var elapsedMs = (Stopwatch.GetTimestamp() - updateStart) * 1000d / Stopwatch.Frequency;
            LogAssistantDebug(currentUpdate, chordDetectionSession.CurrentResult, elapsedMs);
            UpdateChanged?.Invoke(currentUpdate);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SubmitManualChordAsync(string chordText, CancellationToken cancellationToken = default)
    {
        if (!TryParseChordText(chordText, out var chord))
        {
            return;
        }

        var candidate = new RealtimeChordCandidate(
            Chord: chord,
            DisplayName: chord.Name(EnharmonicPreference.Sharps),
            InversionLabel: "Manual input",
            BassPitchClass: chord.RootPitchClass,
            IntervalStructure: chord.Definition.IntervalFormula,
            MatchedPitchClasses: chord.PitchClasses,
            MissingPitchClasses: Array.Empty<int>(),
            ContradictionPitchClasses: Array.Empty<int>(),
            DuplicatePitchClassCounts: new Dictionary<int, int>(),
            CompatibleScales: TonalScaleLibrary.GetCandidateScalesForChord(chord, chord.RootPitchClass, 4)
                .Select(scale => new CandidateScaleSuggestion(
                    scale.RootPitchClass,
                    scale.Mode,
                    scale.Name,
                    scale.IntervalFormula,
                    scale.PitchClasses,
                    scale.Score))
                .ToArray(),
            Score: 1.0,
            Explanation: "Manual chord input");
        var activeNotes = chord.PitchClasses
            .Select(pc => new RealtimeActiveNoteInfo(60 + pc, pc, 0, false))
            .ToArray();
        var snapshot = new RealtimeChordWindowSnapshot(
            ActiveNotes: activeNotes,
            ActivePitchClasses: chord.PitchClasses,
            DuplicatePitchClassCounts: new Dictionary<int, int>(),
            BassPitchClass: chord.RootPitchClass,
            SustainPedalDown: false,
            IsStable: true,
            TooManyNotes: false,
            WindowSpanMs: 0,
            TimestampMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var detection = new RealtimeChordDetectionResult(
            Snapshot: snapshot,
            Candidates: [candidate],
            HasDenseClusterWarning: false,
            WarningMessage: null,
            InferredScaleContext: candidate.CompatibleScales);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updateStart = Stopwatch.GetTimestamp();
            currentUpdate = assistantEngine.Update(detection, assistantEngine.State.Settings, DateTimeOffset.UtcNow);
            var elapsedMs = (Stopwatch.GetTimestamp() - updateStart) * 1000d / Stopwatch.Frequency;
            LogAssistantDebug(currentUpdate, detection, elapsedMs);
            UpdateChanged?.Invoke(currentUpdate);
        }
        finally
        {
            gate.Release();
        }
    }

    private void LogAssistantDebug(
        HarmonicAssistantUpdate update,
        RealtimeChordDetectionResult detection,
        double elapsedMs)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var nowMs = Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;
        var topSuggestion = update.Suggestions.FirstOrDefault();
        var topPath = topSuggestion is null
            ? "(none)"
            : string.Join(" -> ", topSuggestion.Steps.Select(step => step.ChordName));
        var topProbability = topSuggestion?.Probability ?? 0;
        var topDetection = detection.Candidates.FirstOrDefault()?.DisplayName ?? "(none)";
        var topScale = detection.InferredScaleContext.FirstOrDefault()?.Name ?? "(none)";
        var signature = $"{topDetection}|{topScale}|{topPath}|{topProbability:0.000}";

        if (signature == lastDebugSignature && (nowMs - lastDebugTimestampMs) < 2000)
        {
            return;
        }

        lastDebugSignature = signature;
        lastDebugTimestampMs = nowMs;
        logger.LogDebug(
            "Harmonic engine update {LatencyMs:0.0}ms | Top detection {Detection} | Top inferred scale {Scale} | Top suggestion probability {Probability:P1}",
            elapsedMs,
            topDetection,
            topScale,
            topProbability);
    }

    public void Reset()
    {
        assistantEngine.Reset();
        currentUpdate = new HarmonicAssistantUpdate(assistantEngine.State, Array.Empty<HarmonicSuggestionPath>());
        UpdateChanged?.Invoke(currentUpdate);
    }

    private void HandleChordDetectionChanged(RealtimeChordDetectionResult detection)
    {
        _ = QueueUpdateAsync(detection, CancellationToken.None);
    }

    private async Task QueueUpdateAsync(RealtimeChordDetectionResult detection, CancellationToken cancellationToken)
    {
        var version = Interlocked.Increment(ref updateVersion);
        try
        {
            await Task.Delay(95, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != updateVersion)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            currentUpdate = assistantEngine.Update(detection, assistantEngine.State.Settings, DateTimeOffset.UtcNow);
            UpdateChanged?.Invoke(currentUpdate);
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool TryParseChordText(string value, out ChordInstance chord)
    {
        chord = ChordBuilder.Build(0, "maj");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Length < 1)
        {
            return false;
        }

        var rootLength = text.Length >= 2 && (text[1] == '#' || text[1] == 'b' || text[1] == 'B') ? 2 : 1;
        var rootToken = text[..rootLength];
        var symbol = rootLength >= text.Length ? "maj" : text[rootLength..];
        symbol = string.IsNullOrWhiteSpace(symbol) ? "maj" : symbol;

        if (!NoteName.TryParse(rootToken, out var noteName))
        {
            return false;
        }

        if (!ChordLibrary.TryGet(symbol, out _))
        {
            return false;
        }

        chord = ChordBuilder.Build(PitchMath.ToPitchClass(noteName), symbol);
        return true;
    }

    public void Dispose()
    {
        chordDetectionSession.DetectionChanged -= HandleChordDetectionChanged;
        gate.Dispose();
    }
}
