# Graph Editor Sample

This sample contains an example `StateMachineGraphAsset` for the optional
Animator-style graph editor.

## Open the sample

- Double-click `StateMachineGraph.asset`, or
- select it and use **Tools > UnityHFSM > State Machine Editor**, or
- use **Open State Machine Editor** in the asset inspector.

The graph contains a root machine with two regular states and one sub-state
machine. It has no transitions or parameters yet, so it is a good starting
point for experimenting with transitions, conditions and the blackboard.

## Preview without Play Mode

Press **Preview** in the mode bar to run the graph directly in Edit Mode.
No Play Mode, scene object or MonoBehaviour is required. Use **Pause**,
**Step**, **Reset** and the speed selector to inspect the simulation. The graph
is read-only while previewing; **Stop** returns to editing. Active and pending
states are highlighted on the graph, and the **Runtime** Inspector tab shows
live state and condition information.

To run a graph from code, construct a `GraphRuntime` and tick it from your own
update loop. In the editor, live runtimes are discovered automatically in Play
Mode through the editor-only `GraphRuntimeRegistry`, even without a
`StateMachineRunner`.

## Installing from UPM

When the package is installed through the Unity Package Manager, open the
Package Manager, select **UnityHFSM**, open the **Samples** tab and import
**Graph Editor**.
