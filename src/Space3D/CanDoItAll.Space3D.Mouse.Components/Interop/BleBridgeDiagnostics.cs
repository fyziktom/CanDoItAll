namespace CanDoItAll.Space3D.Mouse.Components.Interop;

public sealed class BleBridgeDiagnostics
{
    public bool Supported { get; set; }

    public string State { get; set; } = "unsupported";

    public string SelectedDeviceId { get; set; } = string.Empty;

    public string SelectedDeviceName { get; set; } = string.Empty;

    public bool HasRememberedDevice { get; set; }

    public bool GattConnected { get; set; }

    public bool NotificationsActive { get; set; }

    public string LastError { get; set; } = string.Empty;

    public int FrameCount { get; set; }

    public int RawPacketCount { get; set; }

    public bool DebugCaptureEnabled { get; set; }

    public int DebugPacketCaptureCount { get; set; }
}
