using NUnit.Framework;
using UnityHFSM.Graph.Editor;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphPreviewSession
    {
        [Test]
        public void StartAdvancePauseAndStep()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            using (var preview = new GraphPreviewSession(model))
            {
                Assert.IsTrue(preview.Start());
                Assert.IsTrue(preview.IsRunning);
                Assert.IsNotNull(preview.Runtime);

                preview.Advance(5d);
                preview.Advance(5.1d);
                Assert.AreEqual(0.1d, preview.Runtime.Now, 0.0001d);

                preview.Pause();
                Assert.IsTrue(preview.IsPaused);
                double pausedAt = preview.Runtime.Now;
                preview.Advance(6d);
                Assert.AreEqual(pausedAt, preview.Runtime.Now, 0.0001d);

                preview.Step(0.5f);
                Assert.AreEqual(pausedAt + 0.5d, preview.Runtime.Now, 0.0001d);
            }
        }

        [Test]
        public void RestartPreservesBlackboardValues()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out ParameterDef speed);

            using (var preview = new GraphPreviewSession(model))
            {
                Assert.IsTrue(preview.Start());
                preview.Runtime.SetFloat(speed.name, 3f);

                Assert.IsTrue(preview.Restart(true));
                Assert.AreEqual(3f, preview.Runtime.GetFloat(speed.name), 0.0001f);
            }
        }

        [Test]
        public void InvalidGraphReportsErrorWithoutThrowing()
        {
            using (var preview = new GraphPreviewSession(new GraphModel()))
            {
                Assert.IsFalse(preview.Start());
                Assert.IsNull(preview.Runtime);
                Assert.IsNotEmpty(preview.LastError);
                Assert.IsNotNull(preview.LastValidation);
            }
        }

        [Test]
        public void DisposeStopsAndUnregistersRuntime()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(
                out _,
                out _,
                out _,
                out _);

            var preview = new GraphPreviewSession(model);
            Assert.IsTrue(preview.Start());
            GraphRuntime runtime = preview.Runtime;

            preview.Dispose();

            Assert.IsNull(preview.Runtime);
            Assert.IsFalse(preview.IsRunning);

            var live = new System.Collections.Generic.List<GraphRuntime>();
            GraphRuntimeRegistry.GetLiveRuntimes(live);
            CollectionAssert.DoesNotContain(live, runtime);
        }
    }
}
