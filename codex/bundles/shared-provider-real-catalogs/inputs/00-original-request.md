# Original Request

It is still not done well.
Look at http://localhost:5210/agents?tab=providers at settings of "UI Shared Ollama". from some weird reason it contains models that belongs to openai. if you look at our ollama there should be only models like gptoss20b, gemma, etc. Actual implementation somehow have there openai models. it is wrong.
and in shared openai is also nonsense to define something like "e2e-secondary-model". In openai drivers it must have only definitions of real names of models. The names of models and their price list must reflect reality of provider. not some our made up.
On client side the shared provider must look practically like just mirrored original provider. we do not want to make some our abstractions of models names.
analyze it and repair it. Better to create new bundle for this.
