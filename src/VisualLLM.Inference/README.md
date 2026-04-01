# VisualLLM.Inference

`VisualLLM.Inference` ships the OpenAI-compatible request and response models plus the managed inference abstractions used by VisualLLM.NET.

## Install From GitHub Packages

```bash
dotnet nuget add source --username YOUR_GITHUB_USERNAME --password YOUR_GITHUB_PAT --store-password-in-clear-text --name github "https://nuget.pkg.github.com/mattneel/index.json"
dotnet add package VisualLLM.Inference --version 0.1.1 --source github
```

## What Is In The Package

- `ChatCompletionRequest`, `ChatCompletionResponse`, and related OpenAI-style contract types.
- `IInferenceEngine` and the runtime settings and result types around it.
- Prompt composition helpers used by the in-process runtime.

## What Is Not In The Package

- GGUF model weights.
- Native `llama.cpp` binaries.
- The ASP.NET Core server host.
