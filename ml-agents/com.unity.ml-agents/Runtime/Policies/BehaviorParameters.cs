using Unity.Barracuda;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors.Reflection;

namespace Unity.MLAgents.Policies
{
    /// <summary>
    /// Defines what type of behavior the Agent will be using
    /// </summary>
    [Serializable]
    public enum BehaviorType
    {
        /// <summary>
        /// The Agent will use the remote process for decision making.
        /// if unavailable, will use inference and if no model is provided, will use
        /// the heuristic.
        /// </summary>
        Default,

        /// <summary>
        /// The Agent will always use its heuristic
        /// </summary>
        HeuristicOnly,

        /// <summary>
        /// The Agent will always use inference with the provided
        /// neural network model.
        /// </summary>
        InferenceOnly
    }

    /// <summary>
    /// Options for controlling how the Agent class is searched for <see cref="ObservableAttribute"/>s.
    /// </summary>
    public enum ObservableAttributeOptions
    {
        /// <summary>
        /// All ObservableAttributes on the Agent will be ignored. This is the
        /// default behavior. If there are no  ObservableAttributes on the
        /// Agent, this will result in the fastest initialization time.
        /// </summary>
        Ignore,

        /// <summary>
        /// Only members on the declared class will be examined; members that are
        /// inherited are ignored. This is a reasonable tradeoff between
        /// performance and flexibility.
        /// </summary>
        /// <remarks>This corresponds to setting the
        /// [BindingFlags.DeclaredOnly](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.bindingflags?view=netcore-3.1)
        /// when examining the fields and properties of the Agent class instance.
        /// </remarks>
        ExcludeInherited,

        /// <summary>
        /// All members on the class will be examined. This can lead to slower
        /// startup times.
        /// </summary>
        ExamineAll
    }

    /// <summary>
    /// A component for setting an <seealso cref="Agent"/> instance's behavior and
    /// brain properties.
    /// </summary>
    /// <remarks>At runtime, this component generates the agent's policy objects
    /// according to the settings you specified in the Editor.</remarks>
    [AddComponentMenu("ML Agents/Behavior Parameters", (int)MenuGroup.Default)]
    public class BehaviorParameters : MonoBehaviour
    {
        [HideInInspector, SerializeField]
        BrainParameters m_BrainParameters = new BrainParameters();

        /// <summary>
        /// The associated <see cref="Policies.BrainParameters"/> for this behavior.
        /// </summary>
        public BrainParameters BrainParameters
        {
            get { return m_BrainParameters; }
            internal set { m_BrainParameters = value; }
        }

        [HideInInspector, SerializeField]
        NNModel m_Model;

        /// <summary>
        /// The neural network model used when in inference mode.
        /// This should not be set at runtime; use <see cref="Agent.SetModel(string,NNModel,Policies.InferenceDevice)"/>
        /// to set it instead.
        /// </summary>
        public NNModel Model
        {
            get { return m_Model; }
            set { m_Model = value; UpdateAgentPolicy(); }
        }

        [HideInInspector, SerializeField]
        InferenceDevice m_InferenceDevice;

        /// <summary>
        /// How inference is performed for this Agent's model.
        /// This should not be set at runtime; use <see cref="Agent.SetModel(string,NNModel,Policies.InferenceDevice)"/>
        /// to set it instead.
        /// </summary>
        public InferenceDevice InferenceDevice
        {
            get { return m_InferenceDevice; }
            set { m_InferenceDevice = value; UpdateAgentPolicy(); }
        }

        [HideInInspector, SerializeField]
        BehaviorType m_BehaviorType;

        /// <summary>
        /// The BehaviorType for the Agent.
        /// </summary>
        public BehaviorType BehaviorType
        {
            get { return m_BehaviorType; }
            set { m_BehaviorType = value; UpdateAgentPolicy(); }
        }

        [HideInInspector, SerializeField]
        string m_BehaviorName = "My Behavior";

        /// <summary>
        /// The name of this behavior, which is used as a base name. See
        /// <see cref="FullyQualifiedBehaviorName"/> for the full name.
        /// This should not be set at runtime; use <see cref="Agent.SetModel(string,NNModel,Policies.InferenceDevice)"/>
        /// to set it instead.
        /// </summary>
        public string BehaviorName
        {
            get { return m_BehaviorName; }
            set { m_BehaviorName = value; UpdateAgentPolicy(); }
        }

        /// <summary>
        /// The team ID for this behavior.
        /// </summary>
        [HideInInspector, SerializeField, FormerlySerializedAs("m_TeamID")]
        public int TeamId;
        // TODO properties here instead of Agent

        [FormerlySerializedAs("m_useChildSensors")]
        [HideInInspector]
        [SerializeField]
        [Tooltip("Use all Sensor components attached to child GameObjects of this Agent.")]
        bool m_UseChildSensors = true;

        [HideInInspector]
        [SerializeField]
        [Tooltip("Use all Actuator components attached to child GameObjects of this Agent.")]
        bool m_UseChildActuators = true;

        /// <summary>
        /// Whether or not to use all the sensor components attached to child GameObjects of the agent.
        /// Note that changing this after the Agent has been initialized will not have any effect.
        /// </summary>
        public bool UseChildSensors
        {
            get { return m_UseChildSensors; }
            set { m_UseChildSensors = value; }
        }

        /// <summary>
        /// Whether or not to use all the actuator components attached to child GameObjects of the agent.
        /// Note that changing this after the Agent has been initialized will not have any effect.
        /// </summary>
        public bool UseChildActuators
        {
            get { return m_UseChildActuators; }
            set { m_UseChildActuators = value; }
        }

        [HideInInspector, SerializeField]
        ObservableAttributeOptions m_ObservableAttributeHandling = ObservableAttributeOptions.Ignore;

        /// <summary>
        /// Determines how the Agent class is searched for <see cref="ObservableAttribute"/>s.
        /// </summary>
        public ObservableAttributeOptions ObservableAttributeHandling
        {
            get { return m_ObservableAttributeHandling; }
            set { m_ObservableAttributeHandling = value; }
        }

        /// <summary>
        /// Returns the behavior name, concatenated with any other metadata (i.e. team id).
        /// </summary>
        public string FullyQualifiedBehaviorName
        {
            get { return m_BehaviorName + "?team=" + TeamId; }
        }

        internal IPolicy GeneratePolicy(ActionSpec actionSpec, HeuristicPolicy.ActionGenerator heuristic)
        {
            switch (m_BehaviorType)
            {
                case BehaviorType.HeuristicOnly:
                    return new HeuristicPolicy(heuristic, actionSpec);
                case BehaviorType.InferenceOnly:
                    {
                        if (m_Model == null)
                        {
                            var behaviorType = BehaviorType.InferenceOnly.ToString();
                            throw new UnityAgentsException(
                                $"Can't use Behavior Type {behaviorType} without a model. " +
                                "Either assign a model, or change to a different Behavior Type."
                            );
                        }
                        return new BarracudaPolicy(actionSpec, m_Model, m_InferenceDevice);
                    }
                case BehaviorType.Default:
                    if (Academy.Instance.IsCommunicatorOn)
                    {
                        return new RemotePolicy(actionSpec, FullyQualifiedBehaviorName);
                    }
                    if (m_Model != null)
                    {
                        return new BarracudaPolicy(actionSpec, m_Model, m_InferenceDevice);
                    }
                    else
                    {
                        return new HeuristicPolicy(heuristic, actionSpec);
                    }
                default:
                    return new HeuristicPolicy(heuristic, actionSpec);
            }
        }

        internal void UpdateAgentPolicy()
        {
            var agent = GetComponent<Agent>();
            if (agent == null)
            {
                return;
            }
            agent.ReloadPolicy();
        }
    }
}
