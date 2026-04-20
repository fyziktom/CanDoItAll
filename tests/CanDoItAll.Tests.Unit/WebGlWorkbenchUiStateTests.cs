using CanDoItAll.Components.WebGlLib;

namespace CanDoItAll.Tests.Unit;

public sealed class WebGlWorkbenchUiStateTests
{
    [Fact]
    public void Parse_returns_default_state_when_json_is_invalid()
    {
        var state = WebGlWorkbenchUiState.Parse("{invalid json");

        Assert.Equal(WebGlWorkbenchUiState.CurrentVersion, state.Version);
        Assert.Empty(state.SelectedNodeIds);
        Assert.Equal(WebGlWorkbenchViewPresets.Overview, state.ActiveViewPreset);
        Assert.True(state.DeterministicMode);
        Assert.Equal(WebGlWorkbenchProjectionModes.Orthographic, state.Camera.ProjectionMode);
        Assert.Equal(1180, state.Camera.Distance);
        Assert.Equal(-0.72d, state.Camera.Azimuth);
        Assert.Equal(1.08d, state.Camera.Polar);
    }

    [Fact]
    public void ToJson_normalizes_selection_and_preserves_camera_values()
    {
        var json = new WebGlWorkbenchUiState
        {
            SelectedNodeIds = [" alpha ", "alpha", " ", "beta"],
            ActiveViewPreset = string.Empty,
            DeterministicMode = true,
            ShowDiagnostics = true,
            Camera = new WebGlWorkbenchCameraState
            {
                ProjectionMode = WebGlWorkbenchProjectionModes.Perspective,
                Zoom = 1.4,
                TargetX = 0,
                TargetY = 0,
                TargetZ = 640,
                Distance = 920,
                Azimuth = -0.54d,
                Polar = 1.24d
            }
        }.ToJson();

        var parsed = WebGlWorkbenchUiState.Parse(json);

        Assert.Equal(["alpha", "beta"], parsed.SelectedNodeIds);
        Assert.Equal(WebGlWorkbenchViewPresets.Overview, parsed.ActiveViewPreset);
        Assert.True(parsed.ShowDiagnostics);
        Assert.Equal(WebGlWorkbenchProjectionModes.Perspective, parsed.Camera.ProjectionMode);
        Assert.Equal(1.4, parsed.Camera.Zoom);
        Assert.Equal(0, parsed.Camera.TargetX);
        Assert.Equal(0, parsed.Camera.TargetY);
        Assert.Equal(640, parsed.Camera.TargetZ);
        Assert.Equal(920, parsed.Camera.Distance);
        Assert.Equal(-0.54d, parsed.Camera.Azimuth);
        Assert.Equal(1.24d, parsed.Camera.Polar);
    }
}
