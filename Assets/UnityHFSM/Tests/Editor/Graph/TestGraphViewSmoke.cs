using System.Linq;
using NUnit.Framework;
using UnityHFSM.Graph.Editor;
using UnityEngine;

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

                Assert.AreEqual(5, view.nodes.Count());
                Assert.AreEqual(2, view.edges.Count());
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
