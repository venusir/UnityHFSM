using NUnit.Framework;
using UnityEngine;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphJsonCodec
    {
        [Test]
        public void RoundTripsModelAndBehaviourGuids()
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();

            try
            {
                GraphModel model = GraphTestFactory.CreateBehaviourState(
                    behaviour,
                    out StateDef a,
                    out _,
                    out ParameterDef go);

                string json = GraphJsonCodec.ToJson(model, _ => "behaviour-guid");
                GraphModel imported = GraphJsonCodec.FromJson(
                    json,
                    guid => guid == "behaviour-guid" ? behaviour : null,
                    out GraphValidationResult issues);

                Assert.IsFalse(issues.HasErrors);
                Assert.IsNotNull(imported);
                Assert.AreEqual(model.machines.Count, imported.machines.Count);
                Assert.AreEqual(model.states.Count, imported.states.Count);
                Assert.AreEqual(model.transitions.Count, imported.transitions.Count);
                Assert.AreEqual(model.parameters.Count, imported.parameters.Count);

                StateDef importedA = imported.FindState(a.id);
                Assert.IsNotNull(importedA);
                Assert.AreEqual(1, importedA.behaviours.Count);
                Assert.AreSame(behaviour, importedA.behaviours[0]);
                Assert.AreEqual(go.id, imported.FindParameter(go.id).id);
            }
            finally
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        [Test]
        public void MissingBehaviourGuidIsReported()
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingBehaviour>();

            try
            {
                GraphModel model = GraphTestFactory.CreateBehaviourState(
                    behaviour,
                    out _,
                    out _,
                    out _);

                string json = GraphJsonCodec.ToJson(model, _ => "missing-guid");
                GraphModel imported = GraphJsonCodec.FromJson(json, _ => null, out GraphValidationResult issues);

                Assert.IsNotNull(imported);
                Assert.IsTrue(issues.HasErrors);
            }
            finally
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        [Test]
        public void RejectsWrongFormatAndSchema()
        {
            GraphModel model = GraphTestFactory.CreateTwoState(out _, out _, out _, out _);
            string json = GraphJsonCodec.ToJson(model, null);

            GraphModel wrongFormat = GraphJsonCodec.FromJson(
                json.Replace(GraphJsonCodec.FormatName, "other-format"),
                null,
                out GraphValidationResult formatIssues);
            Assert.IsNull(wrongFormat);
            Assert.IsTrue(formatIssues.HasErrors);

            GraphModel wrongSchema = GraphJsonCodec.FromJson(
                json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"),
                null,
                out GraphValidationResult schemaIssues);
            Assert.IsNull(wrongSchema);
            Assert.IsTrue(schemaIssues.HasErrors);
        }
    }
}
