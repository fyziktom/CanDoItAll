using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

public static class ComfyUiProviderConfigurationKeys
{
    public const string WorkflowTemplateJson = "workflowTemplateJson";
    public const string WorkflowTemplatePath = "workflowTemplatePath";
    public const string PositivePromptNodeId = "positivePromptNodeId";
    public const string PositivePromptInputName = "positivePromptInputName";
    public const string NegativePromptNodeId = "negativePromptNodeId";
    public const string NegativePromptInputName = "negativePromptInputName";
    public const string NegativePrompt = "negativePrompt";
    public const string SamplerNodeId = "samplerNodeId";
    public const string SeedInputName = "seedInputName";
    public const string Seed = "seed";
    public const string RandomizeSeed = "randomizeSeed";
    public const string WidthNodeId = "widthNodeId";
    public const string WidthInputName = "widthInputName";
    public const string HeightNodeId = "heightNodeId";
    public const string HeightInputName = "heightInputName";
    public const string OutputNodeId = "outputNodeId";
    public const string PollIntervalMilliseconds = "pollIntervalMilliseconds";
    public const string TimeoutSeconds = "timeoutSeconds";
    public const string MaxImages = "maxImages";
}

public static class ComfyUiFluxProviderDefaults
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public const string ProviderName = "Local ComfyUI Flux";
    public const string DefaultBaseUrl = "http://127.0.0.1:8188";
    public const string DefaultModel = "flux1-dev.safetensors";
    public const string PositivePromptNodeId = "56:51";
    public const string SamplerNodeId = "56:52";
    public const string LatentSizeNodeId = "56:50";
    public const string OutputNodeId = "9";
    public const int PollIntervalMilliseconds = 1000;
    public const int TimeoutSeconds = 180;
    public const int MaxImages = 1;

    public static IReadOnlyList<string> SuggestedModels { get; } = [DefaultModel];

    public const string WorkflowTemplateJson = """
        {
          "9": {
            "inputs": {
              "filename_prefix": "Flux.1_Dev",
              "images": [
                "56:53",
                0
              ]
            },
            "class_type": "SaveImage",
            "_meta": {
              "title": "Save Image"
            }
          },
          "56:47": {
            "inputs": {
              "vae_name": "ae.safetensors"
            },
            "class_type": "VAELoader",
            "_meta": {
              "title": "Load VAE"
            }
          },
          "56:48": {
            "inputs": {
              "unet_name": "flux1-dev.safetensors",
              "weight_dtype": "default"
            },
            "class_type": "UNETLoader",
            "_meta": {
              "title": "Load Diffusion Model"
            }
          },
          "56:49": {
            "inputs": {
              "clip_name1": "clip_l.safetensors",
              "clip_name2": "t5xxl_fp16.safetensors",
              "type": "flux",
              "device": "default"
            },
            "class_type": "DualCLIPLoader",
            "_meta": {
              "title": "DualCLIPLoader"
            }
          },
          "56:50": {
            "inputs": {
              "width": 1024,
              "height": 1024,
              "batch_size": 1
            },
            "class_type": "EmptySD3LatentImage",
            "_meta": {
              "title": "EmptySD3LatentImage"
            }
          },
          "56:51": {
            "inputs": {
              "text": "Flux image generation prompt",
              "clip": [
                "56:49",
                0
              ]
            },
            "class_type": "CLIPTextEncode",
            "_meta": {
              "title": "CLIP Text Encode (Prompt)"
            }
          },
          "56:52": {
            "inputs": {
              "seed": 1018113035510324,
              "steps": 20,
              "cfg": 1,
              "sampler_name": "euler",
              "scheduler": "simple",
              "denoise": 1,
              "model": [
                "56:48",
                0
              ],
              "positive": [
                "56:51",
                0
              ],
              "negative": [
                "56:54",
                0
              ],
              "latent_image": [
                "56:50",
                0
              ]
            },
            "class_type": "KSampler",
            "_meta": {
              "title": "KSampler"
            }
          },
          "56:53": {
            "inputs": {
              "samples": [
                "56:52",
                0
              ],
              "vae": [
                "56:47",
                0
              ]
            },
            "class_type": "VAEDecode",
            "_meta": {
              "title": "VAE Decode"
            }
          },
          "56:54": {
            "inputs": {
              "conditioning": [
                "56:51",
                0
              ]
            },
            "class_type": "ConditioningZeroOut",
            "_meta": {
              "title": "ConditioningZeroOut"
            }
          }
        }
        """;

    public static string CreateConfigurationJson()
    {
        return JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                [ComfyUiProviderConfigurationKeys.WorkflowTemplateJson] = WorkflowTemplateJson,
                [ComfyUiProviderConfigurationKeys.PositivePromptNodeId] = PositivePromptNodeId,
                [ComfyUiProviderConfigurationKeys.SamplerNodeId] = SamplerNodeId,
                [ComfyUiProviderConfigurationKeys.RandomizeSeed] = true,
                [ComfyUiProviderConfigurationKeys.WidthNodeId] = LatentSizeNodeId,
                [ComfyUiProviderConfigurationKeys.HeightNodeId] = LatentSizeNodeId,
                [ComfyUiProviderConfigurationKeys.OutputNodeId] = OutputNodeId,
                [ComfyUiProviderConfigurationKeys.PollIntervalMilliseconds] = PollIntervalMilliseconds,
                [ComfyUiProviderConfigurationKeys.TimeoutSeconds] = TimeoutSeconds,
                [ComfyUiProviderConfigurationKeys.MaxImages] = MaxImages
            },
            SerializerOptions);
    }
}
