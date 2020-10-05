using System.Collections.Generic;
using Unity.Barracuda;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;


namespace Unity.MLAgents.Inference
{
    /// <summary>
    /// Mapping between the output tensor names and the method that will use the
    /// output tensors and the Agents present in the batch to update their action, memories and
    /// value estimates.
    /// A TensorApplier implements a Dictionary of strings (node names) to an Action.
    /// This action takes as input the tensor and the Dictionary of Agent to AgentInfo for
    /// the current batch.
    /// </summary>
    internal class TensorApplier
    {
        /// <summary>
        /// A tensor Applier's Execute method takes a tensor and a Dictionary of Agent to AgentInfo.
        /// Uses the data contained inside the tensor to modify the state of the Agent. The Tensors
        /// are assumed to have the batch size on the first dimension and the agents to be ordered
        /// the same way in the dictionary and in the tensor.
        /// </summary>
        public interface IApplier
        {
            /// <summary>
            /// Applies the values in the Tensor to the Agents present in the agentInfos
            /// </summary>
            /// <param name="tensorProxy">
            /// The Tensor containing the data to be applied to the Agents
            /// </param>
            /// <param name="actionIds"> List of Agents Ids that will be updated using the tensor's data</param>
            /// <param name="lastActions"> Dictionary of AgentId to Actions to be updated</param>
            void Apply(TensorProxy tensorProxy, IEnumerable<int> actionIds, Dictionary<int, float[]> lastActions);
        }

        readonly Dictionary<string, IApplier> m_Dict = new Dictionary<string, IApplier>();

        /// <summary>
        /// Returns a new TensorAppliers object.
        /// </summary>
        /// <param name="actionSpec"> Description of the action spaces for the Agent.</param>
        /// <param name="seed"> The seed the Appliers will be initialized with.</param>
        /// <param name="allocator"> Tensor allocator</param>
        /// <param name="memories">Dictionary of AgentInfo.id to memory used to pass to the inference model.</param>
        /// <param name="barracudaModel"></param>
        public TensorApplier(
            ActionSpec actionSpec,
            int seed,
            ITensorAllocator allocator,
            Dictionary<int, List<float>> memories,
            object barracudaModel = null)
        {
            actionSpec.CheckNotHybrid();

            if (actionSpec.NumContinuousActions > 0)
            {
                m_Dict[TensorNames.ActionOutput] = new ContinuousActionOutputApplier();
            }
            else
            {
                m_Dict[TensorNames.ActionOutput] =
                    new DiscreteActionOutputApplier(actionSpec.BranchSizes, seed, allocator);
            }
            m_Dict[TensorNames.RecurrentOutput] = new MemoryOutputApplier(memories);

            if (barracudaModel != null)
            {
                var model = (Model)barracudaModel;

                for (var i = 0; i < model?.memories.Count; i++)
                {
                    m_Dict[model.memories[i].output] =
                        new BarracudaMemoryOutputApplier(model.memories.Count, i, memories);
                }
            }
        }

        /// <summary>
        /// Updates the state of the agents based on the data present in the tensor.
        /// </summary>
        /// <param name="tensors"> Enumerable of tensors containing the data.</param>
        /// <param name="actionIds"> List of Agents Ids that will be updated using the tensor's data</param>
        /// <param name="lastActions"> Dictionary of AgentId to Actions to be updated</param>
        /// <exception cref="UnityAgentsException"> One of the tensor does not have an
        /// associated applier.</exception>
        public void ApplyTensors(
            IEnumerable<TensorProxy> tensors, IEnumerable<int> actionIds, Dictionary<int, float[]> lastActions)
        {
            foreach (var tensor in tensors)
            {
                if (!m_Dict.ContainsKey(tensor.name))
                {
                    throw new UnityAgentsException(
                        $"Unknown tensorProxy expected as output : {tensor.name}");
                }
                m_Dict[tensor.name].Apply(tensor, actionIds, lastActions);
            }
        }
    }
}
