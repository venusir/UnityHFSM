# Development

This repository is a **Unity 6000.4.5f1 development project** that embeds the UnityHFSM plugin under `Assets/UnityHFSM/`.

The runtime library remains the same code-first API; the repository layout now follows Unity's standard package structure so it can be opened, tested and extended directly in the Unity Editor.

## Requirements

- Unity **6000.4.5f1** (`ProjectSettings/ProjectVersion.txt` pins the version)
- The `com.unity.test-framework` and `com.unity.ugui` packages (already listed in `Packages/manifest.json`)
- The runtime package itself requires Unity **2021.2+** because the source uses C# 9

## Open the project

1. Open Unity Hub.
2. Choose **Add** and select the repository root (`E:\Projects\UnityHFSM`).
3. Open the project with Unity 6000.4.5f1.

The plugin is compiled as two assemblies:

- `UnityHFSM` — runtime code in `Assets/UnityHFSM/Runtime/`
- `UnityHFSM.Editor` — editor-only code in `Assets/UnityHFSM/Editor/`

## Layout

```text
Assets/
  UnityHFSM/
    Runtime/          # runtime source + UnityHFSM.asmdef
    Editor/           # editor tools + UnityHFSM.Editor.asmdef
    Tests/Editor/     # EditMode tests + UnityHFSM.Tests.asmdef
    Samples/          # GuardAI and Sample3d (visible in the dev project)
    Documentation~/   # documentation assets (not imported by Unity)
    package.json      # package metadata used by the export script
  Dev/                # project-only smoke-test scene/scripts
Packages/             # Unity package manifest
ProjectSettings/      # Unity project settings
Tools/export-upm.ps1  # regenerates a UPM package from Assets/UnityHFSM
```

## Tests

Run the EditMode tests from **Window > General > Test Runner**, or from the command line:

```powershell
& "D:\Program Files\Unity\6000.4.5f1\Editor\Unity.exe" -batchmode `
  -projectPath "E:\Projects\UnityHFSM" `
  -runTests -testPlatform EditMode `
  -testResults "E:\Projects\UnityHFSM\TestResults.xml" `
  -quit -logFile "E:\Projects\UnityHFSM\Logs\tests.log"
```

The tests live in `Assets/UnityHFSM/Tests/Editor/` and use NUnit.

## Samples and smoke test

- `Assets/UnityHFSM/Samples/GuardAI/GuardAI.unity` — the main sample scene.
- `Assets/UnityHFSM/Samples/Sample3d/Scenes/Sample.unity` — the 3D/NavMesh sample.
- `Assets/Dev/Scenes/Playground.unity` — a minimal two-state FSM used as a smoke test.
  If it is missing, run **Tools > UnityHFSM > Create Dev Scene** or call
  `CreateDevScene.Create` in batch mode.

Both samples use the legacy `Input` API, so keep **Project Settings > Player > Active Input Handling** set to **Input Manager (Old)** or **Both**.

## Editor tools

- **Tools > UnityHFSM > Development Info** — verifies the editor assembly references the runtime assembly. This is the placeholder entry point for the visual state machine editor.
- **Tools > UnityHFSM > Create Dev Scene** — creates `Assets/Dev/Scenes/Playground.unity`.

`HfsmAnimatorGraph` now lives in the `UnityHFSM.Editor` assembly. Custom editor assembly definitions that use it must reference `UnityHFSM.Editor`; the default `Assembly-CSharp-Editor` references it automatically.

## Packaging

`master` is the Unity development project and is no longer a UPM package root. To regenerate a package from the plugin folder:

```powershell
pwsh -File Tools/export-upm.ps1
```

The script:

1. copies `Assets/UnityHFSM/` to `dist/com.inspiaaa.unityhfsm/`;
2. renames `Samples` to `Samples~`;
3. copies `README.md`, `CHANGELOG.md` and `LICENSE.md` into the package;
4. validates the sample paths in `package.json`.

`dist/` is ignored by git. Publish the resulting folder to the `upm`/`release` branch or zip it for manual installation.

## Repository notes

- Commit `.meta` files; never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `TestResults.xml` or `dist/`.
- Use `git mv` when moving assets so scene GUID references remain valid.
- The package is code-first; editor tooling should stay under `Assets/UnityHFSM/Editor/` and reference `UnityHFSM`.
