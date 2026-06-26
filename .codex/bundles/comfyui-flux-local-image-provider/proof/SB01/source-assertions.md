# SB01 Source Assertions

## Workflow Shape

- `bundle://inputs/sample/ImageGenerationFlux.json` contains Flux positive prompt node `56:51` with input `text`.
- `bundle://inputs/sample/ImageGenerationFlux.json` contains sampler node `56:52` with input `seed`.
- `bundle://inputs/sample/ImageGenerationFlux.json` contains latent size node `56:50` with inputs `width` and `height`.
- `bundle://inputs/sample/ImageGenerationFlux.json` contains output node `9` with class type `SaveImage`.
- `bundle://inputs/sample/ImageGenerationFlux.json` uses Flux model resources `flux1-dev.safetensors`, `clip_l.safetensors`, `t5xxl_fp16.safetensors`, and `ae.safetensors`.
- The workflow does not contain a negative prompt text node; negative conditioning uses node `56:54` with class type `ConditioningZeroOut`.

## Live Proof Assertions

- `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt` shows ComfyUI health on `http://127.0.0.1:8188`.
- `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt` shows prompt id `e39b85c8-abb7-4a66-9859-b45ff4b5cc47`.
- `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt` shows output file `Flux.1_Dev_00081_.png`.
- `bundle://proof/SB01/generated/flux-live-image.png` is the downloaded image from the current live run.

## Hashes

- `bundle://inputs/sample/ImageGenerationFlux.json`: `c6e602122424bae24bffd91ccfbee7b8ac6e0d661e60b09fda80956b5a7cff1d`
- `bundle://inputs/sample/ComfyUiService.cs`: `9947242d46c93645165d4077a0f83d4fff34662ba2de1523e1c4927e5a452f46`
- `bundle://proof/SB01/transcripts/comfyui-flux-live-generation.txt`: `8079d0e483ba00249e2d5f362373f5b33ecdac1ab334ce474e753f78be11e838`
- `bundle://proof/SB01/generated/flux-live-image.png`: `05a9211ace06587593bb37e2f81e9339736f7067eb3d0cb08fee94ffbc457a72`
