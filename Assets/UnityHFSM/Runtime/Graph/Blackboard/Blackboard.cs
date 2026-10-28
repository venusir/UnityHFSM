using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph
{
    public sealed class BlackboardParameterSnapshot
    {
        public string id;
        public string name;
        public BlackboardParameterType type;
        public float floatValue;
        public int intValue;
        public bool boolValue;
    }

    /// <summary>
    /// Per-runner blackboard. Parameters are addressed by name in the public API
    /// and by resolved index inside condition evaluation.
    /// </summary>
    public sealed class Blackboard
    {
        private readonly BlackboardSchema schema;
        private readonly float[] floats;
        private readonly int[] ints;
        private readonly bool[] bools;
        private readonly bool[] triggers;

        public BlackboardSchema Schema => schema;
        public int Count => schema.Count;

        public Blackboard(BlackboardSchema schema)
        {
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            int count = schema.Count;
            floats = new float[count];
            ints = new int[count];
            bools = new bool[count];
            triggers = new bool[count];

            for (int i = 0; i < count; i++)
            {
                BlackboardParameterInfo parameter = schema.GetParameter(i);
                if (parameter == null)
                    continue;

                floats[i] = parameter.DefaultFloat;
                ints[i] = parameter.DefaultInt;
                bools[i] = parameter.DefaultBool;
            }
        }

        public float GetFloat(string name)
        {
            return GetFloatByIndex(RequireIndex(name));
        }

        public void SetFloat(string name, float value)
        {
            SetFloatByIndex(RequireIndex(name), value);
        }

        public int GetInt(string name)
        {
            return GetIntByIndex(RequireIndex(name));
        }

        public void SetInt(string name, int value)
        {
            SetIntByIndex(RequireIndex(name), value);
        }

        public bool GetBool(string name)
        {
            return GetBoolByIndex(RequireIndex(name));
        }

        public void SetBool(string name, bool value)
        {
            SetBoolByIndex(RequireIndex(name), value);
        }

        public bool GetTrigger(string name)
        {
            return GetTriggerByIndex(RequireIndex(name));
        }

        public void SetTrigger(string name)
        {
            SetTriggerByIndex(RequireIndex(name), true);
        }

        public void ResetTrigger(string name)
        {
            SetTriggerByIndex(RequireIndex(name), false);
        }

        public bool ConsumeTrigger(string name)
        {
            return ConsumeTriggerByIndex(RequireIndex(name));
        }

        public float GetFloatByIndex(int index)
        {
            RequireIndex(index);
            return floats[index];
        }

        public void SetFloatByIndex(int index, float value)
        {
            RequireIndex(index);
            floats[index] = value;
        }

        public int GetIntByIndex(int index)
        {
            RequireIndex(index);
            return ints[index];
        }

        public void SetIntByIndex(int index, int value)
        {
            RequireIndex(index);
            ints[index] = value;
        }

        public bool GetBoolByIndex(int index)
        {
            RequireIndex(index);
            return bools[index];
        }

        public void SetBoolByIndex(int index, bool value)
        {
            RequireIndex(index);
            bools[index] = value;
        }

        public bool GetTriggerByIndex(int index)
        {
            RequireIndex(index);
            return triggers[index];
        }

        public void SetTriggerByIndex(int index, bool value)
        {
            RequireIndex(index);
            triggers[index] = value;
        }

        public bool ConsumeTriggerByIndex(int index)
        {
            RequireIndex(index);
            bool wasSet = triggers[index];
            triggers[index] = false;
            return wasSet;
        }

        public void ResetAllTriggers()
        {
            for (int i = 0; i < triggers.Length; i++)
                triggers[i] = false;
        }

        /// <summary>
        /// Copies values with matching ids and types from another blackboard.
        /// Triggers are intentionally reset instead of copied.
        /// </summary>
        public void CopyValuesFrom(Blackboard other)
        {
            if (other == null)
                return;

            for (int i = 0; i < schema.Count; i++)
            {
                BlackboardParameterInfo target = schema.GetParameter(i);
                if (target == null || !other.schema.TryGetIndexById(target.Id, out int sourceIndex))
                    continue;

                BlackboardParameterInfo source = other.schema.GetParameter(sourceIndex);
                if (source == null || source.Type != target.Type)
                    continue;

                switch (target.Type)
                {
                    case BlackboardParameterType.Float:
                        floats[i] = other.floats[sourceIndex];
                        break;
                    case BlackboardParameterType.Int:
                        ints[i] = other.ints[sourceIndex];
                        break;
                    case BlackboardParameterType.Bool:
                    case BlackboardParameterType.Trigger:
                        bools[i] = other.bools[sourceIndex];
                        break;
                }
            }

            ResetAllTriggers();
        }

        public List<BlackboardParameterSnapshot> Snapshot()
        {
            var snapshot = new List<BlackboardParameterSnapshot>(schema.Count);

            for (int i = 0; i < schema.Count; i++)
            {
                BlackboardParameterInfo parameter = schema.GetParameter(i);
                if (parameter == null)
                    continue;

                var value = new BlackboardParameterSnapshot
                {
                    id = parameter.Id,
                    name = parameter.Name,
                    type = parameter.Type,
                };

                switch (parameter.Type)
                {
                    case BlackboardParameterType.Float:
                        value.floatValue = floats[i];
                        break;
                    case BlackboardParameterType.Int:
                        value.intValue = ints[i];
                        break;
                    case BlackboardParameterType.Bool:
                        value.boolValue = bools[i];
                        break;
                    case BlackboardParameterType.Trigger:
                        value.boolValue = triggers[i];
                        break;
                }

                snapshot.Add(value);
            }

            return snapshot;
        }

        private int RequireIndex(string name)
        {
            if (!schema.TryGetIndexByName(name, out int index))
                throw new ArgumentException("The blackboard has no parameter named '" + name + "'.", nameof(name));
            return index;
        }

        private void RequireIndex(int index)
        {
            if (index < 0 || index >= schema.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
