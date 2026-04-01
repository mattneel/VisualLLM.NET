# Contributing

VisualLLM.NET accepts pull requests from engineers with the composure to ship native interop in Visual Basic without making it somebody else's problem.

## Ground Rules

- Keep the inference path in-process. The backend contract in this repository is `llama.cpp` via P/Invoke, not a subprocess detour with better branding.
- Write production changes in Visual Basic .NET. Auxiliary automation in PowerShell or POSIX shell is fine. Core server code remains Visual Basic.
- Use `Option Explicit On` and `Option Strict On` everywhere. The compiler exists to help, and we have chosen to let it.
- Prefer portable layouts. Native outputs belong in `runtimes/<rid>/native`, where the server and the release scripts already expect to find them.
- Do not "fix" the repository by editing the joke out of it. The tone is part of the project. The implementation still needs to be real.

## Local Workflow

1. Restore and test the managed solution.
   - `dotnet restore VisualLLM.NET.slnx`
   - `dotnet test VisualLLM.NET.slnx`
2. Build the native runtime for your platform.
   - Windows: `./scripts/build-native-runtime.ps1`
   - Linux/macOS: `bash ./scripts/build-native-runtime.sh`
3. Run the native smoke test before sending a pull request.
   - Windows: `./scripts/smoke-native-runtime.ps1`
   - Linux/macOS: `bash ./scripts/smoke-native-runtime.sh`

## Release Layout

- Managed publish output is produced by the packaging scripts.
- Native libraries and the optional `llama-server` sidecar are staged into `runtimes/<rid>/native`.
- GitHub Actions builds `v0.1.0` release artifacts on a cross-platform matrix so breakage has nowhere to hide.

## Pull Requests

- Keep commit messages imperative and free of emoji.
- Include the commands you ran when native or packaging behavior changes.
- If you add a platform-specific workaround, document the constraint in the relevant script instead of leaving it as archaeological evidence for the next maintainer.
