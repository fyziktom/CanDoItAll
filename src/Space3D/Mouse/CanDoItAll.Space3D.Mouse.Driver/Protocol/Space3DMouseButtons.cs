namespace CanDoItAll.Space3D.Mouse.Driver.Protocol;

public enum Space3DMouseButtonAction
{
    None = 0,
    Pan = 1,
    Rotate = 2,
    Zoom = 3,
    PrimaryClick = 4,
    SecondaryClick = 5,
    Calibrate = 6
}

public sealed record Space3DMouseButtonDefinition(
    int ButtonNumber,
    string Name,
    int Millivolts,
    Space3DMouseButtonAction Action);

public sealed record Space3DMouseButtonEvent(
    int ButtonNumber,
    string ButtonName,
    Space3DMouseButtonAction Action,
    MouseAdcEventKind Kind,
    int Sequence,
    int Raw,
    int Millivolts,
    DateTimeOffset ObservedAt)
{
    public string DisplayText => $"{ButtonName} {Kind} @ {(Millivolts / 1000d):0.00} V";
}

public sealed record Space3DMouseButtonSnapshot(
    bool AdcEnabled,
    bool AdcStable,
    MouseAdcSignalState SignalState,
    int CurrentRaw,
    int CurrentMillivolts,
    int? ActiveButtonNumber,
    string ActiveButtonName,
    Space3DMouseButtonAction ActiveAction,
    IReadOnlyList<Space3DMouseButtonEvent> Events)
{
    public static Space3DMouseButtonSnapshot Empty { get; } = new(
        AdcEnabled: false,
        AdcStable: false,
        SignalState: MouseAdcSignalState.Disabled,
        CurrentRaw: 0,
        CurrentMillivolts: 0,
        ActiveButtonNumber: null,
        ActiveButtonName: "none",
        ActiveAction: Space3DMouseButtonAction.None,
        Events: []);

    public bool IsPressed(int buttonNumber)
        => ActiveButtonNumber == buttonNumber &&
           (SignalState == MouseAdcSignalState.Pressed || SignalState == MouseAdcSignalState.Holding);
}

public sealed class Space3DMouseButtonInterpreter
{
    private const int DefaultHistoryLimit = 12;
    private const int DefaultIdleMillivolts = 3000;
    private const int DefaultHysteresisMillivolts = 65;

    private static readonly IReadOnlyList<Space3DMouseButtonDefinition> MeasuredDefinitions =
    [
        new(1, "Button 1", 2600, Space3DMouseButtonAction.Pan),
        new(2, "Button 2", 2000, Space3DMouseButtonAction.Rotate),
        new(3, "Button 3", 1780, Space3DMouseButtonAction.Zoom),
        new(4, "Button 4", 820, Space3DMouseButtonAction.PrimaryClick),
        new(5, "Button 5", 420, Space3DMouseButtonAction.SecondaryClick),
        new(6, "Button 6", 70, Space3DMouseButtonAction.Calibrate)
    ];

    private readonly IReadOnlyList<Space3DMouseButtonDefinition> definitions;
    private readonly IReadOnlyList<Space3DMouseButtonDefinition> definitionsByVoltage;
    private readonly Queue<Space3DMouseButtonEvent> eventHistory = new();
    private readonly int historyLimit;
    private readonly int idleMillivolts;
    private readonly int hysteresisMillivolts;
    private int? activeButtonNumber;
    private int? lastEventSequence;
    private Space3DMouseButtonSnapshot latestSnapshot = Space3DMouseButtonSnapshot.Empty;

    public Space3DMouseButtonInterpreter(
        IReadOnlyList<Space3DMouseButtonDefinition>? definitions = null,
        int historyLimit = DefaultHistoryLimit,
        int idleMillivolts = DefaultIdleMillivolts,
        int hysteresisMillivolts = DefaultHysteresisMillivolts)
    {
        this.definitions = (definitions is { Count: > 0 } ? definitions : MeasuredDefinitions)
            .OrderBy(definition => definition.ButtonNumber)
            .ToArray();
        definitionsByVoltage = this.definitions
            .OrderBy(definition => definition.Millivolts)
            .ToArray();
        this.historyLimit = Math.Max(5, historyLimit);
        this.idleMillivolts = Math.Max(1, idleMillivolts);
        this.hysteresisMillivolts = Math.Max(0, hysteresisMillivolts);
    }

    public static Space3DMouseButtonInterpreter CreateMeasuredLadder()
        => new();

    public IReadOnlyList<Space3DMouseButtonDefinition> Definitions => definitions;

    public Space3DMouseButtonSnapshot LatestSnapshot => latestSnapshot;

    public Space3DMouseButtonSnapshot Update(MouseTelemetrySnapshot? telemetry)
    {
        if (telemetry is null)
        {
            latestSnapshot = Space3DMouseButtonSnapshot.Empty with
            {
                Events = eventHistory.Reverse().ToArray()
            };
            return latestSnapshot;
        }

        activeButtonNumber = ResolveActiveButton(telemetry);

        if (telemetry.AdcEnabled &&
            telemetry.AdcEventKind != MouseAdcEventKind.None &&
            lastEventSequence != telemetry.AdcEventSequence)
        {
            var eventButtonNumber = ResolveButtonNumber(telemetry.AdcEventMillivolts, activeButtonNumber);
            if (eventButtonNumber is int buttonNumber &&
                ResolveDefinition(buttonNumber) is { } definition)
            {
                EnqueueEvent(new Space3DMouseButtonEvent(
                    definition.ButtonNumber,
                    definition.Name,
                    definition.Action,
                    telemetry.AdcEventKind,
                    telemetry.AdcEventSequence,
                    telemetry.AdcEventRaw,
                    telemetry.AdcEventMillivolts,
                    telemetry.ReceivedAt));
            }

            lastEventSequence = telemetry.AdcEventSequence;
        }

        var activeDefinition = activeButtonNumber is int active
            ? ResolveDefinition(active)
            : null;
        latestSnapshot = new Space3DMouseButtonSnapshot(
            telemetry.AdcEnabled,
            telemetry.AdcStable,
            telemetry.AdcSignalState,
            telemetry.AdcRaw,
            telemetry.AdcMillivolts,
            activeDefinition?.ButtonNumber,
            activeDefinition?.Name ?? "none",
            activeDefinition?.Action ?? Space3DMouseButtonAction.None,
            eventHistory.Reverse().ToArray());
        return latestSnapshot;
    }

    private int? ResolveActiveButton(MouseTelemetrySnapshot telemetry)
    {
        if (!telemetry.AdcEnabled)
        {
            return null;
        }

        return telemetry.AdcSignalState switch
        {
            MouseAdcSignalState.Pressed or MouseAdcSignalState.Holding
                => ResolveButtonNumber(telemetry.AdcMillivolts, activeButtonNumber),
            _ => null
        };
    }

    private int? ResolveButtonNumber(int millivolts, int? previousButtonNumber)
    {
        if (definitionsByVoltage.Count == 0 || millivolts >= idleMillivolts)
        {
            return null;
        }

        if (previousButtonNumber is int previous &&
            ResolveDefinition(previous) is { } previousDefinition &&
            IsInsideRetainedRange(previousDefinition, millivolts))
        {
            return previous;
        }

        var bestDefinition = definitionsByVoltage
            .OrderBy(definition => Math.Abs(definition.Millivolts - millivolts))
            .First();

        return IsInsideClassificationRange(bestDefinition, millivolts)
            ? bestDefinition.ButtonNumber
            : null;
    }

    private bool IsInsideRetainedRange(Space3DMouseButtonDefinition definition, int millivolts)
    {
        var index = IndexOfVoltageDefinition(definition.ButtonNumber);
        var lower = index == 0
            ? int.MinValue
            : Midpoint(definitionsByVoltage[index - 1].Millivolts, definition.Millivolts) - hysteresisMillivolts;
        var upper = index == definitionsByVoltage.Count - 1
            ? idleMillivolts + hysteresisMillivolts
            : Midpoint(definition.Millivolts, definitionsByVoltage[index + 1].Millivolts) + hysteresisMillivolts;
        return millivolts >= lower && millivolts <= upper;
    }

    private bool IsInsideClassificationRange(Space3DMouseButtonDefinition definition, int millivolts)
    {
        var index = IndexOfVoltageDefinition(definition.ButtonNumber);
        var lower = index == 0
            ? int.MinValue
            : Midpoint(definitionsByVoltage[index - 1].Millivolts, definition.Millivolts);
        var upper = index == definitionsByVoltage.Count - 1
            ? idleMillivolts
            : Midpoint(definition.Millivolts, definitionsByVoltage[index + 1].Millivolts);
        return millivolts >= lower && millivolts < upper;
    }

    private int IndexOfVoltageDefinition(int buttonNumber)
    {
        for (var index = 0; index < definitionsByVoltage.Count; index++)
        {
            if (definitionsByVoltage[index].ButtonNumber == buttonNumber)
            {
                return index;
            }
        }

        return 0;
    }

    private Space3DMouseButtonDefinition? ResolveDefinition(int buttonNumber)
        => definitions.FirstOrDefault(definition => definition.ButtonNumber == buttonNumber);

    private void EnqueueEvent(Space3DMouseButtonEvent buttonEvent)
    {
        eventHistory.Enqueue(buttonEvent);
        while (eventHistory.Count > historyLimit)
        {
            eventHistory.Dequeue();
        }
    }

    private static int Midpoint(int first, int second)
        => (first + second) / 2;
}
