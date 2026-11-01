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
- `UnityHFSM.Graph.Editor` — Session/Commands/Views graph editor, runtime preview and debugging UI

The code-first API remains the primary, fully supported workflow. The graph layer is additive and depends on `UnityHFSM`; the core assembly never depends on the graph layer.

## Layout

```text
Assets/
  UnityHFSM/
    Runtime/          # runtime source + UnityHFSM.asmdef
      Graph/          # optional graph asset/runtime layer + UnityHFSM.Graph.asmdef
    Editor/           # editor tools + UnityHFSM.Editor.asmdef
      Graph/          # GraphView editor + UnityHFSM.Graph.Editor.asmdef
        Session/      # GraphEditorSession, mode, selection, navigation, layout state
        Commands/     # document/navigation/authoring/runtime/layout/diagnostics commands
        Views/        # document bar, mode bar, parameters, inspector, console, layout
        GraphView/    # canvas, nodes, edges
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
2. The window has two header rows. **Document** contains the asset, follow/lock, Back/Forward, breadcrumb and **Find**. **Mode** contains commands for the current mode. Selecting a graph asset, double-clicking it, or selecting a GameObject with an optional `StateMachineRunner` follows its graph.
3. Modes are explicit: **Edit**, **Preview**, and **Live** in Play Mode. Preview/Live are read-only for graph structure; selection, navigation, zoom and live parameter values remain available.
4. Drag the splitter next to **Parameters** / **Inspector** or the Console splitter to resize panes. The **View** menu toggles panels, grid, framing and zoom; layout state is serialized on the window.
5. In Edit mode use **+ Add** or right-click for states/sub-state machines, drag ports or use **Link** for transitions, and **Delete** for the selection. **Find** (`Ctrl+F`) searches states, machines and transitions. Breadcrumb history supports Back/Forward.
6. Manage parameters in **Parameters**: in Edit mode **+** adds Float/Int/Bool/Trigger and edits defaults; in Preview/Live the panel switches to live values and Trigger Set/Reset.
7. Select a transition and add conditions; conditions reference blackboard parameters. Create `StateBehaviour` ScriptableObject subclasses and assign them on a state.
8. Press **Preview** in the Mode bar. No MonoBehaviour or Play Mode is required. Pause/Step/Reset/Stop and speed control the simulation; graph highlights, the **Runtime** Inspector tab and the Console show what happened. Stop Preview to edit again.
9. To debug code-created runtimes, construct `GraphRuntime` normally and enter Play Mode. The editor-only `GraphRuntimeRegistry` discovers matching runtimes; choose a target in the Mode bar. `StateMachineRunner` remains supported for scene-hosted graphs.
10. The **Console** is collapsed by default. **Validation** and **Runtime Log** live there; click an issue or log entry to navigate. Keyboard shortcuts are listed in the README and in tooltips.

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
