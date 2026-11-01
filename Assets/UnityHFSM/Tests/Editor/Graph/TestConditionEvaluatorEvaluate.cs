using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestConditionEvaluatorEvaluate
    {
        [Test]
        public void EvaluateIsNonConsumingAndEvaluateAndConsumeConsumesTriggers()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out _);
            ParameterDef go = GraphTestFactory.AddParameter(model, "Go", BlackboardParameterType.Bool);
            ParameterDef speed = GraphTestFactory.AddParameter(model, "Speed", BlackboardParameterType.Float);
            ParameterDef fire = GraphTestFactory.AddParameter(model, "Fire", BlackboardParameterType.Trigger);

            var schema = new BlackboardSchema(model.parameters);
            var evaluator = new ConditionEvaluator(schema, new[]
            {
                GraphTestFactory.Condition(go, ConditionComparison.IsTrue),
                GraphTestFactory.Condition(speed, ConditionComparison.Greater, 1f),
                GraphTestFactory.Condition(fire, ConditionComparison.IsSet),
            });

            var blackboard = new Blackboard(schema);
            blackboard.SetBool(go.name, true);
            blackboard.SetFloat(speed.name, 2f);
            blackboard.SetTrigger(fire.name);

            Assert.AreEqual(3, evaluator.Count);
            Assert.IsTrue(evaluator.Evaluate(blackboard));
            Assert.IsTrue(blackboard.GetTrigger(fire.name), "Evaluate must not consume triggers.");
            Assert.IsTrue(evaluator.EvaluateCondition(0, blackboard));
            Assert.IsTrue(evaluator.EvaluateCondition(2, blackboard));

            Assert.IsTrue(evaluator.EvaluateAndConsume(blackboard));
            Assert.IsFalse(blackboard.GetTrigger(fire.name), "EvaluateAndConsume must consume triggers.");
        }

        [Test]
        public void PerConditionEvaluationDoesNotConsumeTriggers()
        {
            GraphModel model = GraphTestFactory.CreateRoot(out _);
            ParameterDef fire = GraphTestFactory.AddParameter(model, "Fire", BlackboardParameterType.Trigger);

            var schema = new BlackboardSchema(model.parameters);
            var evaluator = new ConditionEvaluator(schema, new[]
            {
                GraphTestFactory.Condition(fire, ConditionComparison.IsSet),
            });

            var blackboard = new Blackboard(schema);
            blackboard.SetTrigger(fire.name);

            Assert.IsTrue(evaluator.EvaluateCondition(0, blackboard));
            Assert.IsTrue(blackboard.GetTrigger(fire.name));
            Assert.IsFalse(evaluator.EvaluateCondition(1, blackboard));
        }
    }
}
