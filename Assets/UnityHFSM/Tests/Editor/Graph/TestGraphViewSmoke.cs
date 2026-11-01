using System.Linq;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityHFSM.Graph.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphViewSmoke
    {
        [Test]
        public void LoadMachineCreatesNodesAndEdges()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                var controller = new GraphEditorController(asset);
                string rootId = asset.Graph.rootMachineId;
                string firstStateId = asset.Graph.states[0].id;
                string secondStateId = controller.AddState(rootId, new Vector2(200f, 0f));

                controller.AddTransition(
                    rootId,
                    TransitionSourceKind.State,
                    firstStateId,
                    TransitionTargetKind.State,
                    secondStateId);

                var view = new StateMachineGraphView
                {
                    Controller = controller,
                };

                view.LoadMachine(rootId);

                Assert.AreEqual(4, view.nodes.Count()); // Entry, Any State, two states; no root Exit
                Assert.AreEqual(2, view.edges.Count());
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateStateAtRefreshesStandaloneViewAndSelectsNode()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                var controller = new GraphEditorController(asset);
                string rootId = asset.Graph.rootMachineId;

                var view = new StateMachineGraphView { Controller = controller };
                view.LoadMachine(rootId);
                int nodeCountBefore = view.nodes.Count();

                string stateId = view.CreateStateAt(new Vector2(300f, 120f));

                Assert.IsFalse(string.IsNullOrEmpty(stateId));
                Assert.AreEqual(nodeCountBefore + 1, view.nodes.Count());

                StateGraphNode node = view.nodes
                    .OfType<StateGraphNode>()
                    .Single(n => n.StateId == stateId);

                Assert.AreEqual(new Vector2(300f, 120f), node.GetPosition().position);
                Assert.AreEqual(1, view.selection.Count);
                Assert.AreSame(node, view.selection[0]);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void CreateSubMachineAtCreatesHostNode()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                var controller = new GraphEditorController(asset);
                string rootId = asset.Graph.rootMachineId;

                var view = new StateMachineGraphView { Controller = controller };
                view.LoadMachine(rootId);

                string hostId = view.CreateSubMachineAt(new Vector2(100f, 50f));

                StateDef host = controller.FindState(hostId);
                Assert.IsNotNull(host);
                Assert.AreEqual(StateKind.HybridSubMachine, host.kind);
                Assert.IsFalse(string.IsNullOrEmpty(host.subMachineId));

                StateGraphNode node = view.nodes
                    .OfType<StateGraphNode>()
                    .Single(n => n.StateId == hostId);

                Assert.IsTrue(node.IsSubMachine);
                Assert.AreEqual(1, view.selection.Count);
                Assert.AreSame(node, view.selection[0]);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ReadOnlyBlocksAuthoringAndRestoresWhenDisabled()
        {
            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();

            try
            {
                asset.SetGraph(GraphModelFactory.CreateNew());
                var controller = new GraphEditorController(asset);
                var view = new StateMachineGraphView { Controller = controller };
                view.LoadMachine(asset.Graph.rootMachineId);

                view.SetReadOnly(true);

                Assert.IsNull(view.CreateStateAt(Vector2.zero));
                Assert.AreEqual(EventPropagation.Stop, view.DeleteSelection());

                StateGraphNode node = view.nodes.OfType<StateGraphNode>().First();
                Assert.IsFalse((node.capabilities & Capabilities.Movable) != 0);
                Assert.IsFalse((node.capabilities & Capabilities.Deletable) != 0);
                Assert.AreEqual(PickingMode.Ignore, node.InputPort.pickingMode);

                view.SetReadOnly(false);

                Assert.IsTrue((node.capabilities & Capabilities.Movable) != 0);
                Assert.AreEqual(PickingMode.Position, node.InputPort.pickingMode);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RootMachineHasNoExitPseudoNodeButChildMachineDoes()
        {
            GraphModel model = GraphTestFactory.CreateNested(
                out MachineDef root,
                out _,
                out MachineDef child,
                out _,
                out _,
                out _);

            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();

            try
            {
                asset.SetGraph(model);
                var controller = new GraphEditorController(asset);
                var view = new StateMachineGraphView { Controller = controller };

                view.LoadMachine(root.id);
                Assert.IsFalse(view.nodes.OfType<PseudoGraphNode>()
                    .Any(node => node.Kind == PseudoNodeKind.Exit));

                view.LoadMachine(child.id);
                Assert.IsTrue(view.nodes.OfType<PseudoGraphNode>()
                    .Any(node => node.Kind == PseudoNodeKind.Exit));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
