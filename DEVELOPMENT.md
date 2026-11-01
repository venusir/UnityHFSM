# Development

This repository is a **Unity 6000.4.5f1 development project** that embeds the UnityHFSM plugin under `Assets/UnityHFSM/`.

The runtime library remains the same code-first API; the repository layout now follows Unity's standard package structure so it can be opened, tested and extended directly in the Unity Editor.

## Requirements

- Unity **6000.4.5f1** (`ProjectSettings/ProjectVersion.txt` pins the version)
- The `com.unity.test-framework` package (already listed in `Packages/manifest.json`)
- The runtime package itself requires Unity **2021.2+** because the source uses C# 9

## Open the project

1. Open Unity Hub.
2. Choose **Add** and select the repository root (`E:\Projects\UnityHFSM`).
3. Open the project with Unity 6000.4.5f1.

The plugin is compiled as these assemblies:

- `UnityHFSM` — core runtime code in `Assets/UnityHFSM/Runtime/`
- `UnityHFSM.Graph` — optional graph-asset runtime layer in `Assets/UnityHFSM/Runtime/Graph/`
- `UnityHFSM.Editor` — existing editor tools (Animator graph, development info)
- `UnityHFSM.Graph.Editor` — GraphView editor, blackboard and runtime debug UI

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
    Samples/          # Graph Editor sample (visible in the dev project)
    Documentation~/   # documentation assets (not imported by Unity)
    package.json      # package metadata used by the export script
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

## Samples

- `Assets/UnityHFSM/Samples/GraphEditor/StateMachineGraph.asset` — the Graph Editor sample.
  Open it with **Tools > UnityHFSM > State Machine Editor**, or double-click the asset.
  See the sample's `README.md` for the preview workflow and UPM installation notes.

The sample is exported with the package (`Samples~`); the manual smoke tests that
used to live outside the package were replaced by the EditMode test suite.

## Editor tools

- **Tools > UnityHFSM > State Machine Editor** — opens the Animator-style GraphView editor.
- **Assets > Create > UnityHFSM > State Machine Graph** — creates a new graph asset with a root machine and one default state.
- **Tools > UnityHFSM > Development Info** — verifies the editor assembly references the runtime assembly.

`HfsmAnimatorGraph` now lives in the `UnityHFSM.Editor` assembly. Custom editor assembly definitions that use it must reference `UnityHFSM.Editor`; the default `Assembly-CSharp-Editor` references it automatically.

## Graph editor workflow

The graph editor is optional; code-first state machines continue to work exactly as before.

1. Create a `StateMachineGraphAsset` and open it in **Tools > UnityHFSM > State Machine Editor**.
2. Open a graph by selecting a `StateMachineGraphAsset` in the Project window, double-clicking it, or using **Open State Machine Editor** in its inspector. Selecting a GameObject with an optional `StateMachineRunner` also follows its graph. The toolbar shows the breadcrumb, **Lock**, preview controls, search and validation badge.
3. Drag the splitter next to **Parameters** / **Inspector** or the bottom drawer splitter to resize panes. Use the **⋯** menu for panel visibility, grid, framing and zoom. Layout state is serialized on the window.
4. Right-click the canvas to create states and sub-state machines or choose **Frame All**; drag transitions. Use **Find** (`Ctrl+F`) to search states, machines and transitions.
5. Manage parameters in the **Parameters** panel: **+** adds Float/Int/Bool/Trigger, names and values are edited inline, right-click deletes a row, and types are fixed after creation.
6. Select a transition and add conditions; conditions reference blackboard parameters.
7. Create `StateBehaviour` ScriptableObject subclasses for state logic and assign them on a state.
8. Press **Play** in the toolbar to preview in Edit Mode. No MonoBehaviour or Play Mode is required. Pause/Step/Restart and speed control the preview; graph highlights, live parameter values, the live Inspector and the Runtime Log drawer show what happened.
9. To debug code-created runtimes, construct `GraphRuntime` normally and enter Play Mode. The editor-only `GraphRuntimeRegistry` discovers live runtimes automatically; select one from the toolbar target dropdown. `StateMachineRunner` is still supported for scene-hosted, auto-ticked graphs.
10. The bottom drawer (**Validation** / **Runtime Log**) is collapsed by default. Click validation issues or transition-log entries to navigate to the corresponding node or edge.

Graph runtime limitations in this version: animation clips, blend trees, avatar masks, layers and transition blend durations are intentionally out of scope. Importing an existing code-built FSM into the graph asset is not supported; the graph layer uses its own ScriptableObject-based asset format.

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
