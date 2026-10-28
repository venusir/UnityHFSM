using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Runtime-safe JSON conversion. Behaviour GUID resolution is injected by the
    /// caller so this codec can live in the runtime assembly and be unit tested.
    /// </summary>
    public static class GraphJsonCodec
    {
        public const string FormatName = "unityhfsm.graph";
        public const int CurrentSchemaVersion = 1;

        public static string ToJson(GraphModel model, Func<StateBehaviour, string> behaviourToGuid)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            model.EnsureLists();

            var dto = new GraphDocumentDto
            {
                format = FormatName,
                schemaVersion = CurrentSchemaVersion,
                graphId = model.graphId,
                rootMachineId = model.rootMachineId,
            };

            for (int i = 0; i < model.machines.Count; i++)
            {
                MachineDef machine = model.machines[i];
                if (machine == null)
                    continue;

                dto.machines.Add(new DtoMachine
                {
                    id = machine.id,
                    name = machine.name,
                    parentStateId = machine.parentStateId,
                    startStateId = machine.startStateId,
                    kind = machine.kind,
                    rememberLastState = machine.rememberLastState,
                    entryPosition = machine.entryPosition,
                    anyStatePosition = machine.anyStatePosition,
                    exitPosition = machine.exitPosition,
                });
            }

            for (int i = 0; i < model.states.Count; i++)
            {
                StateDef state = model.states[i];
                if (state == null)
                    continue;

                var dtoState = new DtoState
                {
                    id = state.id,
                    machineId = state.machineId,
                    name = state.name,
                    kind = state.kind,
                    subMachineId = state.subMachineId,
                    needsExitTime = state.needsExitTime,
                    isGhostState = state.isGhostState,
                    position = state.position,
                };

                if (state.behaviours != null)
                {
                    for (int b = 0; b < state.behaviours.Count; b++)
                    {
                        StateBehaviour behaviour = state.behaviours[b];
                        string guid = behaviour != null && behaviourToGuid != null
                            ? behaviourToGuid(behaviour)
                            : string.Empty;

                        dtoState.behaviourGuids.Add(guid ?? string.Empty);
                    }
                }

                dto.states.Add(dtoState);
            }

            for (int i = 0; i < model.transitions.Count; i++)
            {
                TransitionDef transition = model.transitions[i];
                if (transition == null)
                    continue;

                var dtoTransition = new DtoTransition
                {
                    id = transition.id,
                    machineId = transition.machineId,
                    source = transition.source,
                    fromStateId = transition.fromStateId,
                    target = transition.target,
                    toStateId = transition.toStateId,
                    delaySeconds = transition.delaySeconds,
                    forceInstantly = transition.forceInstantly,
                    order = transition.order,
                };

                if (transition.conditions != null)
                {
                    for (int c = 0; c < transition.conditions.Count; c++)
                    {
                        ConditionDef condition = transition.conditions[c];
                        if (condition == null)
                            continue;

                        dtoTransition.conditions.Add(new DtoCondition
                        {
                            parameterId = condition.parameterId,
                            comparison = condition.comparison,
                            floatValue = condition.floatValue,
                            intValue = condition.intValue,
                        });
                    }
                }

                dto.transitions.Add(dtoTransition);
            }

            for (int i = 0; i < model.parameters.Count; i++)
            {
                ParameterDef parameter = model.parameters[i];
                if (parameter == null)
                    continue;

                dto.parameters.Add(new DtoParameter
                {
                    id = parameter.id,
                    name = parameter.name,
                    type = parameter.type,
                    defaultFloat = parameter.defaultFloat,
                    defaultInt = parameter.defaultInt,
                    defaultBool = parameter.defaultBool,
                    exposed = parameter.exposed,
                });
            }

            return JsonUtility.ToJson(dto, true);
        }

        public static GraphModel FromJson(
            string json,
            Func<string, StateBehaviour> resolveBehaviour,
            out GraphValidationResult issues)
        {
            issues = new GraphValidationResult();

            if (string.IsNullOrWhiteSpace(json))
            {
                issues.AddError("json-empty", "The JSON document is empty.");
                return null;
            }

            GraphDocumentDto dto;
            try
            {
                dto = JsonUtility.FromJson<GraphDocumentDto>(json);
            }
            catch (Exception exception)
            {
                issues.AddError("json-parse", "Could not parse graph JSON: " + exception.Message);
                return null;
            }

            if (dto == null)
            {
                issues.AddError("json-parse", "Could not parse graph JSON.");
                return null;
            }

            if (dto.format != FormatName)
            {
                issues.AddError("json-format", "Unsupported graph JSON format: '" + dto.format + "'.");
                return null;
            }

            if (dto.schemaVersion != CurrentSchemaVersion)
            {
                issues.AddError("json-schema", "Unsupported graph JSON schema version: " + dto.schemaVersion + ".");
                return null;
            }

            var model = new GraphModel
            {
                schemaVersion = dto.schemaVersion,
                graphId = dto.graphId ?? string.Empty,
                rootMachineId = dto.rootMachineId ?? string.Empty,
            };

            if (dto.machines != null)
            {
                for (int i = 0; i < dto.machines.Count; i++)
                {
                    DtoMachine source = dto.machines[i];
                    if (source == null)
                        continue;

                    model.machines.Add(new MachineDef
                    {
                        id = source.id ?? string.Empty,
                        name = source.name ?? string.Empty,
                        parentStateId = source.parentStateId ?? string.Empty,
                        startStateId = source.startStateId ?? string.Empty,
                        kind = source.kind,
                        rememberLastState = source.rememberLastState,
                        entryPosition = source.entryPosition,
                        anyStatePosition = source.anyStatePosition,
                        exitPosition = source.exitPosition,
                    });
                }
            }

            if (dto.states != null)
            {
                for (int i = 0; i < dto.states.Count; i++)
                {
                    DtoState source = dto.states[i];
                    if (source == null)
                        continue;

                    var state = new StateDef
                    {
                        id = source.id ?? string.Empty,
                        machineId = source.machineId ?? string.Empty,
                        name = source.name ?? string.Empty,
                        kind = source.kind,
                        subMachineId = source.subMachineId ?? string.Empty,
                        needsExitTime = source.needsExitTime,
                        isGhostState = source.isGhostState,
                        position = source.position,
                    };

                    if (source.behaviourGuids != null)
                    {
                        for (int b = 0; b < source.behaviourGuids.Count; b++)
                        {
                            string guid = source.behaviourGuids[b];
                            if (string.IsNullOrEmpty(guid))
                                continue;

                            StateBehaviour behaviour = resolveBehaviour != null ? resolveBehaviour(guid) : null;
                            if (behaviour == null)
                            {
                                issues.AddError(
                                    "json-behaviour-missing",
                                    "Behaviour asset GUID '" + guid + "' could not be resolved.",
                                    stateId: state.id);
                                continue;
                            }

                            state.behaviours.Add(behaviour);
                        }
                    }

                    model.states.Add(state);
                }
            }

            if (dto.transitions != null)
            {
                for (int i = 0; i < dto.transitions.Count; i++)
                {
                    DtoTransition source = dto.transitions[i];
                    if (source == null)
                        continue;

                    var transition = new TransitionDef
                    {
                        id = source.id ?? string.Empty,
                        machineId = source.machineId ?? string.Empty,
                        source = source.source,
                        fromStateId = source.fromStateId ?? string.Empty,
                        target = source.target,
                        toStateId = source.toStateId ?? string.Empty,
                        delaySeconds = source.delaySeconds,
                        forceInstantly = source.forceInstantly,
                        order = source.order,
                    };

                    if (source.conditions != null)
                    {
                        for (int c = 0; c < source.conditions.Count; c++)
                        {
                            DtoCondition condition = source.conditions[c];
                            if (condition == null)
                                continue;

                            transition.conditions.Add(new ConditionDef
                            {
                                parameterId = condition.parameterId ?? string.Empty,
                                comparison = condition.comparison,
                                floatValue = condition.floatValue,
                                intValue = condition.intValue,
                            });
                        }
                    }

                    model.transitions.Add(transition);
                }
            }

            if (dto.parameters != null)
            {
                for (int i = 0; i < dto.parameters.Count; i++)
                {
                    DtoParameter source = dto.parameters[i];
                    if (source == null)
                        continue;

                    model.parameters.Add(new ParameterDef
                    {
                        id = source.id ?? string.Empty,
                        name = source.name ?? string.Empty,
                        type = source.type,
                        defaultFloat = source.defaultFloat,
                        defaultInt = source.defaultInt,
                        defaultBool = source.defaultBool,
                        exposed = source.exposed,
                    });
                }
            }

            model.EnsureLists();
            return model;
        }
    }
}
