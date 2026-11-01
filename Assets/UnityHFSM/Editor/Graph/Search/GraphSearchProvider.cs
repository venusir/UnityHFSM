using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace UnityHFSM.Graph.Editor
{
    public sealed class GraphSearchResult
    {
        public string MachineId;
        public string StateId;
        public string TransitionId;
    }

    /// <summary>
    /// Search/jump palette provider. SearchWindow requires a ScriptableObject
    /// provider in Unity 2021.2, so this type is created with CreateInstance and
    /// destroyed with the window.
    /// </summary>
    public sealed class GraphSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private GraphEditorController controller;
        private Action<GraphSearchResult> selected;

        public void Initialize(GraphEditorController controller, Action<GraphSearchResult> selected)
        {
            this.controller = controller;
            this.selected = selected;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>();
            if (controller == null || controller.Model == null)
                return entries;

            GraphModel model = controller.Model;

            entries.Add(new SearchTreeGroupEntry(new GUIContent("States")));
            for (int i = 0; i < model.states.Count; i++)
            {
                StateDef state = model.states[i];
                if (state == null)
                    continue;

                string machinePath = GraphNavigation.BuildMachinePath(model, state.machineId);
                string label = string.IsNullOrEmpty(machinePath)
                    ? state.name
                    : machinePath + " / " + state.name;

                entries.Add(new SearchTreeEntry(new GUIContent(label))
                {
                    level = 1,
                    userData = new GraphSearchResult
                    {
                        MachineId = state.machineId,
                        StateId = state.id,
                    },
                });
            }

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Machines")));
            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef machine = model.machines[i];
                if (machine == null)
                    continue;

                string path = GraphNavigation.BuildMachinePath(model, machine.id);
                entries.Add(new SearchTreeEntry(new GUIContent(
                    string.IsNullOrEmpty(path) ? "(unnamed)" : path))
                {
                    level = 1,
                    userData = new GraphSearchResult
                    {
                        MachineId = machine.id,
                    },
                });
            }

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Transitions")));
            for (int i = 0; i < model.transitions.Count; i++)
            {
                TransitionDef transition = model.transitions[i];
                if (transition == null)
                    continue;

                string machinePath = GraphNavigation.BuildMachinePath(model, transition.machineId);
                StateDef from = transition.source == TransitionSourceKind.State
                    ? model.FindState(transition.fromStateId)
                    : null;
                StateDef to = transition.target == TransitionTargetKind.State
                    ? model.FindState(transition.toStateId)
                    : null;

                string fromName = from != null ? from.name : "Any State";
                string toName = transition.target == TransitionTargetKind.Exit
                    ? "Exit"
                    : to != null ? to.name : "(missing)";
                string label = (string.IsNullOrEmpty(machinePath) ? string.Empty : machinePath + " / ")
                    + fromName + " -> " + toName;

                entries.Add(new SearchTreeEntry(new GUIContent(label))
                {
                    level = 1,
                    userData = new GraphSearchResult
                    {
                        MachineId = transition.machineId,
                        TransitionId = transition.id,
                    },
                });
            }

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry == null || !(entry.userData is GraphSearchResult result))
                return false;

            selected?.Invoke(result);
            return true;
        }
    }
}
