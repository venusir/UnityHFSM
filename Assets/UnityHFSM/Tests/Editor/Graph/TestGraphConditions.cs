using NUnit.Framework;

namespace UnityHFSM.Graph.Tests
{
    [TestFixture]
    [Category("Graph")]
    public class TestGraphConditions
    {
        private static ConditionEvaluator CreateEvaluator(
            BlackboardSchema schema,
            ConditionDef condition)
        {
            return new ConditionEvaluator(schema, new[] { condition });
        }

        [Test]
        public void FloatComparisons()
        {
            var parameter = new ParameterDef
            {
                id = "p",
                name = "P",
                type = BlackboardParameterType.Float,
            };

            var schema = new BlackboardSchema(new[] { parameter });
            var blackboard = new Blackboard(schema);
            ConditionEvaluator greater = CreateEvaluator(schema, new ConditionDef
            {
                parameterId = "p",
                comparison = ConditionComparison.Greater,
                floatValue = 1f,
            });

            blackboard.SetFloat("P", 2f);
            Assert.IsTrue(greater.EvaluateAndConsume(blackboard));

            blackboard.SetFloat("P", 1f);
            Assert.IsFalse(greater.EvaluateAndConsume(blackboard));

            ConditionEvaluator equal = CreateEvaluator(schema, new ConditionDef
            {
                parameterId = "p",
                comparison = ConditionComparison.Equal,
                floatValue = 1.000001f,
            });

            blackboard.SetFloat("P", 1f);
            Assert.IsTrue(equal.EvaluateAndConsume(blackboard));
        }

        [Test]
        public void IntAndBoolComparisons()
        {
            var intParameter = new ParameterDef
            {
                id = "i",
                name = "I",
                type = BlackboardParameterType.Int,
            };
            var boolParameter = new ParameterDef
            {
                id = "b",
                name = "B",
                type = BlackboardParameterType.Bool,
            };

            var schema = new BlackboardSchema(new[] { intParameter, boolParameter });
            var blackboard = new Blackboard(schema);
            blackboard.SetInt("I", 3);
            blackboard.SetBool("B", true);

            var intLess = CreateEvaluator(schema, new ConditionDef
            {
                parameterId = "i",
                comparison = ConditionComparison.Less,
                intValue = 4,
            });
            var boolTrue = CreateEvaluator(schema, new ConditionDef
            {
                parameterId = "b",
                comparison = ConditionComparison.IsTrue,
            });

            Assert.IsTrue(intLess.EvaluateAndConsume(blackboard));
            Assert.IsTrue(boolTrue.EvaluateAndConsume(blackboard));
        }

        [Test]
        public void EmptyConditionListIsAlwaysTrue()
        {
            var schema = new BlackboardSchema(new ParameterDef[0]);
            var blackboard = new Blackboard(schema);
            var evaluator = new ConditionEvaluator(schema, null);

            Assert.IsTrue(evaluator.EvaluateAndConsume(blackboard));
        }
    }
}
