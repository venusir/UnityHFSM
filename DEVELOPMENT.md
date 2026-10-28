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

The plugin is compiled as these assemblies:

- `UnityHFSM` — core runtime code in `Assets/UnityHFSM/Runtime/`
- `UnityHFSM.Graph` — optional graph-asset runtime layer in `Assets/UnityHFSM/Runtime/Graph/`
- `UnityHFSM.Editor` — existing editor tools (Animator graph, development info)
- `UnityHFSM.Graph.Editor` — GraphView editor, blackboard, JSON import/export and runtime debug UI

The code-first API remains the primary, fully supported workflow. The graph layer is additive and depends on `UnityHFSM`; the core assembly never depends on the graph layer.

## Layout

```text
Assets/
  UnityHFSM/
    Runtime/          # runtime source + UnityHFSM.asmdef
      Graph/          # optional graph asset/runtime layer + UnityHFSM.Graph.asmdef
    Editor/           # editor tools + UnityHFSM.Editor.asmdef
      Graph/          # GraphView editor + UnityHFSM.Graph.Editor.asmdef
    Tests/Editor/     # EditMode tests + UnityHFSM.Tests.asmdef
      Graph/          # graph runtime/editor tests + UnityHFSM.Graph.Tests.asmdef
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
  -logFile "E:\Projects\UnityHFSM\Logs\tests.log"
```

Do **not** add `-quit`: Test Framework 1.6 can terminate before the test run when it is present. The tests live in `Assets/UnityHFSM/Tests/Editor/` and use NUnit.

## Samples and smoke test

- `Assets/UnityHFSM/Samples/GuardAI/GuardAI.unity` — the main sample scene.
- `Assets/UnityHFSM/Samples/Sample3d/Scenes/Sample.unity` — the 3D/NavMesh sample.
- `Assets/Dev/Scenes/Playground.unity` — a minimal two-state FSM used as a smoke test.
  If it is missing, run **Tools > UnityHFSM > Create Dev Scene** or call
  `CreateDevScene.Create` in batch mode.

Both samples use the legacy `Input` API, so keep **Project Settings > Player > Active Input Handling** set to **Input Manager (Old)** or **Both**.

## Editor tools

- **Tools > UnityHFSM > State Machine Editor** — opens the Animator-style GraphView editor.
- **Assets > Create > UnityHFSM > State Machine Graph** — creates a new graph asset with a root machine and one default state.
- **Tools > UnityHFSM > Development Info** — verifies the editor assembly references the runtime assembly.
- **Tools > UnityHFSM > Create Dev Scene** — creates `Assets/Dev/Scenes/Playground.unity`.

`HfsmAnimatorGraph` now lives in the `UnityHFSM.Editor` assembly. Custom editor assembly definitions that use it must reference `UnityHFSM.Editor`; the default `Assembly-CSharp-Editor` references it automatically.

## Graph editor workflow

The graph editor is optional; code-first state machines continue to work exactly as before.

1. Create a `StateMachineGraphAsset` and open it in **Tools > UnityHFSM > State Machine Editor**.
2. Add states/sub-state machines, drag transitions, and add blackboard parameters (Float/Int/Bool/Trigger).
3. Select a transition and add conditions; conditions reference blackboard parameters.
4. Create `StateBehaviour` ScriptableObject subclasses for state logic and assign them on a state.
5. Add a `StateMachineRunner` to a GameObject, assign the graph asset, and enter Play Mode.
6. In the editor window, select the runner to see active/pending highlights, blackboard values and the transition log; use Pause/Step/Rebuild for debugging.
7. Export/Import JSON from the toolbar for review, backup or transfer.

Graph runtime limitations in this version: animation clips, blend trees, avatar masks, layers and transition blend durations are intentionally out of scope. Importing an existing code-built FSM into the graph asset is not supported; the graph layer has its own JSON format.

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
- The package remains code-first; the graph layer is an optional additive workflow.
- Editor tooling should stay under `Assets/UnityHFSM/Editor/` and reference `UnityHFSM` (and `UnityHFSM.Graph` when working with graph assets).
