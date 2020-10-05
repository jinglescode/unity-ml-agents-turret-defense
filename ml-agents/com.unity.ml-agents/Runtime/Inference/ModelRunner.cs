using System.Collections.Generic;
using Unity.Barracuda;
using UnityEngine.Profiling;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

namespace Unity.MLAgents.Inference
{
    internal struct AgentInfoSensorsPair
    {
        public AgentInfo agentInfo;
        public List<ISensor> sensors;
    }

    internal class ModelRunner
    {
        List<AgentInfoSensorsPair> m_Infos = new List<AgentInfoSensorsPair>();
        Dictionary<int, float[]> m_LastActionsReceived = new Dictionary<int, float[]>();
        List<int> m_OrderedAgentsRequestingDecisions = new List<int>();

        ITensorAllocator m_TensorAllocator;
        TensorGenerator m_TensorGenerator;
        TensorApplier m_TensorApplier;

        NNModel m_Model;
        InferenceDevice m_InferenceDevice;
        IWorker m_Engine;
        bool m_Verbose = false;
        string[] m_OutputNames;
        IReadOnlyList<TensorProxy> m_InferenceInputs;
        IReadOnlyList<TensorProxy> m_InferenceOutputs;
        Dictionary<int, List<float>> m_Memories = new Dictionary<int, List<float>>();

        SensorShapeValidator m_SensorShapeValidator = new SensorShapeValidator();

        bool m_VisualObservationsInitialized;

        /// <summary>
        /// Initializes the Brain with the Model that it will use when selecting actions for
        /// the agents
        /// </summary>
        /// <param name="model"> The Barracuda model to load </param>
        /// <param name="actionSpec"> Description of the action spaces for the Agent.</param>
        /// <param name="inferenceDevice"> Inference execution device. CPU is the fastest
        /// option for most of ML Agents models. </param>
        /// <param name="seed"> The seed that will be used to initialize the RandomNormal
        /// and Multinomial objects used when running inference.</param>
        /// <exception cref="UnityAgentsException">Throws an error when the model is null
        /// </exception>
        public ModelRunner(
            NNModel model,
            ActionSpec actionSpec,
            InferenceDevice inferenceDevice = InferenceDevice.CPU,
            int seed = 0)
        {
            Model barracudaModel;
            m_Model = model;
            m_InferenceDevice = inferenceDevice;
            m_TensorAllocator = new TensorCachingAllocator();
            if (model != null)
            {
#if BARRACUDA_VERBOSE
                m_Verbose = true;
#endif

                D.logEnabled = m_Verbose;

                barracudaModel = ModelLoader.Load(model);
                var executionDevice = inferenceDevice == InferenceDevice.GPU
                    ? WorkerFactory.Type.ComputePrecompiled
                    : WorkerFactory.Type.CSharp;
                m_Engine = WorkerFactory.CreateWorker(executionDevice, barracudaModel, m_Verbose);
            }
            else
            {
                barracudaModel = null;
                m_Engine = null;
            }

            m_InferenceInputs = BarracudaModelParamLoader.GetInputTensors(barracudaModel);
            m_OutputNames = BarracudaModelParamLoader.GetOutputNames(barracudaModel);
            m_TensorGenerator = new TensorGenerator(
                seed, m_TensorAllocator, m_Memories, barracudaModel);
            m_TensorApplier = new TensorApplier(
                actionSpec, seed, m_TensorAllocator, m_Memories, barracudaModel);
        }

        static Dictionary<string, Tensor> PrepareBarracudaInputs(IEnumerable<TensorProxy> infInputs)
        {
            var inputs = new Dictionary<string, Tensor>();
            foreach (var inp in infInputs)
            {
                inputs[inp.name] = inp.data;
            }

            return inputs;
        }

        public void Dispose()
        {
            if (m_Engine != null)
                m_Engine.Dispose();
            m_TensorAllocator?.Reset(false);
        }

        List<TensorProxy> FetchBarracudaOutputs(string[] names)
        {
            var outputs = new List<TensorProxy>();
            foreach (var n in names)
            {
                var output = m_Engine.PeekOutput(n);
                outputs.Add(TensorUtils.TensorProxyFromBarracuda(output, n));
            }

            return outputs;
        }

        public void PutObservations(AgentInfo info, List<ISensor> sensors)
        {
#if DEBUG
            m_SensorShapeValidator.ValidateSensors(sensors);
#endif
            m_Infos.Add(new AgentInfoSensorsPair
            {
                agentInfo = info,
                sensors = sensors
            });

            // We add the episodeId to this list to maintain the order in which the decisions were requested
            m_OrderedAgentsRequestingDecisions.Add(info.episodeId);

            if (!m_LastActionsReceived.ContainsKey(info.episodeId))
            {
                m_LastActionsReceived[info.episodeId] = null;
            }
            if (info.done)
            {
                // If the agent is done, we remove the key from the last action dictionary since no action
                // should be taken.
                m_LastActionsReceived.Remove(info.episodeId);
            }
        }

        public void DecideBatch()
        {
            var currentBatchSize = m_Infos.Count;
            if (currentBatchSize == 0)
            {
                return;
            }
            if (!m_VisualObservationsInitialized)
            {
                // Just grab the first agent in the collection (any will suffice, really).
                // We check for an empty Collection above, so this will always return successfully.
                var firstInfo = m_Infos[0];
                m_TensorGenerator.InitializeObservations(firstInfo.sensors, m_TensorAllocator);
                m_VisualObservationsInitialized = true;
            }

            Profiler.BeginSample("ModelRunner.DecideAction");

            Profiler.BeginSample($"MLAgents.{m_Model.name}.GenerateTensors");
            // Prepare the input tensors to be feed into the engine
            m_TensorGenerator.GenerateTensors(m_InferenceInputs, currentBatchSize, m_Infos);
            Profiler.EndSample();

            Profiler.BeginSample($"MLAgents.{m_Model.name}.PrepareBarracudaInputs");
            var inputs = PrepareBarracudaInputs(m_InferenceInputs);
            Profiler.EndSample();

            // Execute the Model
            Profiler.BeginSample($"MLAgents.{m_Model.name}.ExecuteGraph");
            m_Engine.Execute(inputs);
            Profiler.EndSample();

            Profiler.BeginSample($"MLAgents.{m_Model.name}.FetchBarracudaOutputs");
            m_InferenceOutputs = FetchBarracudaOutputs(m_OutputNames);
            Profiler.EndSample();

            Profiler.BeginSample($"MLAgents.{m_Model.name}.ApplyTensors");
            // Update the outputs
            m_TensorApplier.ApplyTensors(m_InferenceOutputs, m_OrderedAgentsRequestingDecisions, m_LastActionsReceived);
            Profiler.EndSample();

            Profiler.EndSample();

            m_Infos.Clear();

            m_OrderedAgentsRequestingDecisions.Clear();
        }

        public bool HasModel(NNModel other, InferenceDevice otherInferenceDevice)
        {
            return m_Model == other && m_InferenceDevice == otherInferenceDevice;
        }

        public float[] GetAction(int agentId)
        {
            if (m_LastActionsReceived.ContainsKey(agentId))
            {
                return m_LastActionsReceived[agentId];
            }
            return null;
        }
    }
}
