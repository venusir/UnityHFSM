using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Compiled condition list for one transition. Conditions are ANDed and
    /// trigger parameters are consumed only after every condition passes.
    /// </summary>
    public sealed class ConditionEvaluator
    {
        private readonly CompiledCondition[] conditions;
        private readonly List<int> passedTriggerIndices = new List<int>();

        public ConditionEvaluator(BlackboardSchema schema, IList<ConditionDef> definitions)
        {
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));

            if (definitions == null || definitions.Count == 0)
            {
                conditions = Array.Empty<CompiledCondition>();
                return;
            }

            var compiled = new List<CompiledCondition>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                ConditionDef definition = definitions[i];
                if (definition == null)
                    continue;

                if (!schema.TryGetIndexById(definition.parameterId, out int index))
                {
                    throw new ArgumentException(
                        "Condition references unknown parameter '" + definition.parameterId + "'.",
                        nameof(definitions));
                }

                BlackboardParameterInfo parameter = schema.GetParameter(index);
                compiled.Add(new CompiledCondition(
                    index,
                    parameter.Type,
                    definition.comparison,
                    definition.floatValue,
                    definition.intValue));
            }

            conditions = compiled.ToArray();
        }

        /// <summary>Number of compiled conditions.</summary>
        public int Count => conditions.Length;

        /// <summary>
        /// Evaluates all conditions without consuming trigger parameters. Useful
        /// for editor inspection and preview UIs.
        /// </summary>
        public bool Evaluate(Blackboard blackboard)
        {
            return EvaluateInternal(blackboard, false);
        }

        public bool EvaluateAndConsume(Blackboard blackboard)
        {
            return EvaluateInternal(blackboard, true);
        }

        /// <summary>
        /// Evaluates one condition by index without consuming triggers.
        /// </summary>
        public bool EvaluateCondition(int conditionIndex, Blackboard blackboard)
        {
            if (blackboard == null)
                throw new ArgumentNullException(nameof(blackboard));

            if (conditionIndex < 0 || conditionIndex >= conditions.Length)
                return false;

            return EvaluateCompiledCondition(conditions[conditionIndex], blackboard);
        }

        private bool EvaluateInternal(Blackboard blackboard, bool consumeTriggers)
        {
            if (blackboard == null)
                throw new ArgumentNullException(nameof(blackboard));

            if (conditions.Length == 0)
                return true;

            passedTriggerIndices.Clear();

            for (int i = 0; i < conditions.Length; i++)
            {
                CompiledCondition condition = conditions[i];

                if (!EvaluateCompiledCondition(condition, blackboard))
                    return false;

                if (consumeTriggers && condition.type == BlackboardParameterType.Trigger)
                    passedTriggerIndices.Add(condition.index);
            }

            if (consumeTriggers)
            {
                for (int i = 0; i < passedTriggerIndices.Count; i++)
                    blackboard.ConsumeTriggerByIndex(passedTriggerIndices[i]);
            }

            return true;
        }

        private static bool EvaluateCompiledCondition(CompiledCondition condition, Blackboard blackboard)
        {
            switch (condition.type)
            {
                case BlackboardParameterType.Float:
                    return CompareFloat(blackboard.GetFloatByIndex(condition.index), condition);

                case BlackboardParameterType.Int:
                    return CompareInt(blackboard.GetIntByIndex(condition.index), condition);

                case BlackboardParameterType.Bool:
                    return CompareBool(blackboard.GetBoolByIndex(condition.index), condition.comparison);

                case BlackboardParameterType.Trigger:
                    return condition.comparison == ConditionComparison.IsSet
                        && blackboard.GetTriggerByIndex(condition.index);

                default:
                    return false;
            }
        }

        private static bool CompareFloat(float value, CompiledCondition condition)
        {
            switch (condition.comparison)
            {
                case ConditionComparison.Equal:
                    return Math.Abs(value - condition.floatValue) <= 0.00001f;
                case ConditionComparison.NotEqual:
                    return Math.Abs(value - condition.floatValue) > 0.00001f;
                case ConditionComparison.Less:
                    return value < condition.floatValue;
                case ConditionComparison.LessOrEqual:
                    return value <= condition.floatValue;
                case ConditionComparison.Greater:
                    return value > condition.floatValue;
                case ConditionComparison.GreaterOrEqual:
                    return value >= condition.floatValue;
                default:
                    return false;
            }
        }

        private static bool CompareInt(int value, CompiledCondition condition)
        {
            switch (condition.comparison)
            {
                case ConditionComparison.Equal:
                    return value == condition.intValue;
                case ConditionComparison.NotEqual:
                    return value != condition.intValue;
                case ConditionComparison.Less:
                    return value < condition.intValue;
                case ConditionComparison.LessOrEqual:
                    return value <= condition.intValue;
                case ConditionComparison.Greater:
                    return value > condition.intValue;
                case ConditionComparison.GreaterOrEqual:
                    return value >= condition.intValue;
                default:
                    return false;
            }
        }

        private static bool CompareBool(bool value, ConditionComparison comparison)
        {
            switch (comparison)
            {
                case ConditionComparison.IsTrue:
                    return value;
                case ConditionComparison.IsFalse:
                    return !value;
                default:
                    return false;
            }
        }

        private readonly struct CompiledCondition
        {
            public readonly int index;
            public readonly BlackboardParameterType type;
            public readonly ConditionComparison comparison;
            public readonly float floatValue;
            public readonly int intValue;

            public CompiledCondition(
                int index,
                BlackboardParameterType type,
                ConditionComparison comparison,
                float floatValue,
                int intValue)
            {
                this.index = index;
                this.type = type;
                this.comparison = comparison;
                this.floatValue = floatValue;
                this.intValue = intValue;
            }
        }
    }
}
