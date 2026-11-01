using NUnit.Framework;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphSelectionAndHistory
    {
        [Test]
        public void SelectionFactoriesSupportSinglePseudoAndMulti()
        {
            GraphEditorSelection state = GraphEditorSelection.State("a");
            GraphEditorSelection transition = GraphEditorSelection.Transition("t");
            GraphEditorSelection pseudo = GraphEditorSelection.Pseudo(PseudoNodeKind.AnyState);
            GraphEditorSelection machine = GraphEditorSelection.Machine("m");

            Assert.AreEqual(GraphEditorSelectionKind.State, state.Kind);
            Assert.AreEqual(GraphEditorSelectionKind.Transition, transition.Kind);
            Assert.AreEqual(GraphEditorSelectionKind.Pseudo, pseudo.Kind);
            Assert.AreEqual(GraphEditorSelectionKind.Machine, machine.Kind);
            Assert.IsTrue(state.HasSelection);
            Assert.IsFalse(GraphEditorSelection.None.HasSelection);

            Assert.AreEqual(
                state,
                GraphEditorSelection.Multi(new[]
                {
                    new GraphEditorSelectionItem(GraphEditorSelectionItemKind.State, "a"),
                    new GraphEditorSelectionItem(GraphEditorSelectionItemKind.State, "a"),
                }));

            GraphEditorSelection multi = GraphEditorSelection.Multi(new[]
            {
                new GraphEditorSelectionItem(GraphEditorSelectionItemKind.Transition, "t"),
                new GraphEditorSelectionItem(GraphEditorSelectionItemKind.State, "a"),
                new GraphEditorSelectionItem(GraphEditorSelectionItemKind.Pseudo, "Entry", PseudoNodeKind.Entry),
            });

            Assert.AreEqual(GraphEditorSelectionKind.Multi, multi.Kind);
            Assert.AreEqual(3, multi.Items.Count);
            Assert.IsTrue(multi.Contains(
                new GraphEditorSelectionItem(GraphEditorSelectionItemKind.State, "a")));
        }

        [Test]
        public void SelectionDoesNotEqualDifferentIds()
        {
            Assert.AreNotEqual(GraphEditorSelection.State("a"), GraphEditorSelection.State("b"));
            Assert.AreNotEqual(GraphEditorSelection.State("a"), GraphEditorSelection.None);
        }

        [Test]
        public void HistorySupportsBackForwardTruncationAndCap()
        {
            var history = new GraphEditorNavigationHistory();
            GraphEditorNavigationTarget first =
                new GraphEditorNavigationTarget("a", GraphEditorSelection.State("s1"));
            GraphEditorNavigationTarget second =
                new GraphEditorNavigationTarget("b", GraphEditorSelection.State("s2"));
            GraphEditorNavigationTarget third =
                new GraphEditorNavigationTarget("c", GraphEditorSelection.State("s3"));

            history.Push(first);
            history.Push(first);
            history.Push(second);
            history.Push(third);

            Assert.AreEqual(3, history.Count);
            Assert.IsTrue(history.TryBack(out GraphEditorNavigationTarget back));
            Assert.AreEqual(second, back);
            Assert.IsTrue(history.CanGoForward);

            history.Push(first);
            Assert.AreEqual(3, history.Count);
            Assert.IsFalse(history.CanGoForward);

            for (int i = 0; i < 80; i++)
            {
                history.Push(new GraphEditorNavigationTarget(
                    "m" + i,
                    GraphEditorSelection.State("s" + i)));
            }

            Assert.LessOrEqual(history.Count, GraphEditorNavigationHistory.Capacity);
        }
    }
}
