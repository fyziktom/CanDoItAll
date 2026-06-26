namespace ZyphoNote.MarketingPrompts.Options;

public sealed class ComfyUiOptions
{
    public const string SectionName = "ComfyUI";

    public string BaseUrl { get; set; } = "http://192.168.10.132:8188";
    public string WorkflowPath { get; set; } = "..\\..\\..\\flux_dev_checkpoint_lady.json";
    public string PositivePromptNodeId { get; set; } = "56:51";
    public string? NegativePromptNodeId { get; set; }
    public string? SamplerNodeId { get; set; } = "56:52";
    public string OutputDirectory { get; set; } = "wwwroot/generated/comfyui";
    public string PublicOutputPath { get; set; } = "/generated/comfyui";
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 180;
}
