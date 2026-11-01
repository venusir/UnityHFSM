using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphNavigationAndSearch
    {
        [Test]
        public void BuildsMachineChainAndResolvesIssues()
        {
            GraphModel model = GraphTestFactory.CreateNested(
                out MachineDef root,
                out _,
                out MachineDef child,
                out StateDef childA,
                out _,
                out _);

            List<MachineDef> chain = GraphNavigation.BuildMachineChain(model, child.id);
            Assert.AreEqual(2, chain.Count);
            Assert.AreEqual(root.id, chain[0].id);
            Assert.AreEqual(child.id, chain[1].id);
            Assert.AreEqual("Root / SubMachine", GraphNavigation.BuildMachinePath(model, child.id));

            Assert.AreEqual(child.id, GraphNavigation.ResolveMachineIdForState(model, childA.id));
            Assert.AreEqual(child.id, GraphNavigation.ResolveMachineIdForTransition(
                model,
                model.transitions[model.transitions.Count - 1].id));
            Assert.AreEqual(
                root.id,
                GraphNavigation.ResolveMachineIdForIssue(
                    model,
                    new GraphIssue(GraphIssueSeverity.Warning, "test", "message", machineId: root.id)));
        }

        [Test]
        public void SearchProviderListsAndSelectsStates()
        {
            GraphModel model = GraphTestFactory.CreateNested(
                out _,
                out _,
                out MachineDef child,
                out StateDef childA,
                out _,
                out _);

            var asset = ScriptableObject.CreateInstance<StateMachineGraphAsset>();
            var provider = ScriptableObject.CreateInstance<GraphSearchProvider>();

            try
            {
                asset.SetGraph(model);
                var controller = new GraphEditorController(asset);
                GraphSearchResult selected = null;
                provider.Initialize(controller, result => selected = result);

                List<SearchTreeEntry> entries = provider.CreateSearchTree(
                    new SearchWindowContext(Vector2.zero));

                Assert.IsTrue(entries.Count > 3);

                SearchTreeEntry match = entries.Find(entry =>
                    entry.userData is GraphSearchResult result
                    && result.StateId == childA.id);

                Assert.IsNotNull(match);
                Assert.IsTrue(provider.OnSelectEntry(match, new SearchWindowContext(Vector2.zero)));
                Assert.IsNotNull(selected);
                Assert.AreEqual(child.id, selected.MachineId);
                Assert.AreEqual(childA.id, selected.StateId);
            }
            finally
            {
                Object.DestroyImmediate(provider);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
