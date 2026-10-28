using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph
{
    public sealed class BlackboardParameterInfo
    {
        public string Id { get; }
        public string Name { get; }
        public BlackboardParameterType Type { get; }
        public float DefaultFloat { get; }
        public int DefaultInt { get; }
        public bool DefaultBool { get; }

        public BlackboardParameterInfo(
            string id,
            string name,
            BlackboardParameterType type,
            float defaultFloat,
            int defaultInt,
            bool defaultBool)
        {
            Id = id;
            Name = name;
            Type = type;
            DefaultFloat = defaultFloat;
            DefaultInt = defaultInt;
            DefaultBool = defaultBool;
        }
    }

    /// <summary>
    /// Resolved parameter lookup for one graph. Definitions are copied so asset
    /// edits cannot affect a running runtime.
    /// </summary>
    public sealed class BlackboardSchema
    {
        private readonly List<BlackboardParameterInfo> parameters;
        private readonly Dictionary<string, int> indexById;
        private readonly Dictionary<string, int> indexByName;

        public IReadOnlyList<BlackboardParameterInfo> Parameters => parameters;
        public int Count => parameters.Count;

        public BlackboardSchema(IEnumerable<ParameterDef> definitions)
        {
            parameters = new List<BlackboardParameterInfo>();
            indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            indexByName = new Dictionary<string, int>(StringComparer.Ordinal);

            if (definitions == null)
                return;

            foreach (ParameterDef definition in definitions)
            {
                if (definition == null)
                    continue;

                int index = parameters.Count;
                parameters.Add(new BlackboardParameterInfo(
                    definition.id,
                    definition.name,
                    definition.type,
                    definition.defaultFloat,
                    definition.defaultInt,
                    definition.defaultBool));

                if (!string.IsNullOrEmpty(definition.id))
                    indexById[definition.id] = index;

                if (!string.IsNullOrEmpty(definition.name))
                    indexByName[definition.name] = index;
            }
        }

        public bool TryGetIndexById(string id, out int index)
        {
            index = -1;
            return !string.IsNullOrEmpty(id) && indexById.TryGetValue(id, out index);
        }

        public bool TryGetIndexByName(string name, out int index)
        {
            index = -1;
            return !string.IsNullOrEmpty(name) && indexByName.TryGetValue(name, out index);
        }

        public BlackboardParameterInfo GetParameter(int index)
        {
            return index >= 0 && index < parameters.Count ? parameters[index] : null;
        }
    }
}
