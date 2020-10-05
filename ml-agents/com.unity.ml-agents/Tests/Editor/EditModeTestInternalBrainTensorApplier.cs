using System.Collections.Generic;
using NUnit.Framework;
using Unity.Barracuda;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Inference;
using Unity.MLAgents.Policies;

namespace Unity.MLAgents.Tests
{
    public class EditModeTestInternalBrainTensorApplier
    {
        class TestAgent : Agent
        {
        }

        [Test]
        public void Construction()
        {
            var actionSpec = new ActionSpec();
            var alloc = new TensorCachingAllocator();
            var mem = new Dictionary<int, List<float>>();
            var tensorGenerator = new TensorApplier(actionSpec, 0, alloc, mem);
            Assert.IsNotNull(tensorGenerator);
            alloc.Dispose();
        }

        [Test]
        public void ApplyContinuousActionOutput()
        {
            var inputTensor = new TensorProxy()
            {
                shape = new long[] { 2, 3 },
                data = new Tensor(2, 3, new float[] { 1, 2, 3, 4, 5, 6 })
            };

            var applier = new ContinuousActionOutputApplier();

            var agentIds = new List<int>() { 0, 1 };
            // Dictionary from AgentId to Action
            var actionDict = new Dictionary<int, float[]>() { { 0, null }, { 1, null } };

            applier.Apply(inputTensor, agentIds, actionDict);


            Assert.AreEqual(actionDict[0][0], 1);
            Assert.AreEqual(actionDict[0][1], 2);
            Assert.AreEqual(actionDict[0][2], 3);

            Assert.AreEqual(actionDict[1][0], 4);
            Assert.AreEqual(actionDict[1][1], 5);
            Assert.AreEqual(actionDict[1][2], 6);
        }

        [Test]
        public void ApplyDiscreteActionOutput()
        {
            var inputTensor = new TensorProxy()
            {
                shape = new long[] { 2, 5 },
                data = new Tensor(
                    2,
                    5,
                    new[] { 0.5f, 22.5f, 0.1f, 5f, 1f, 4f, 5f, 6f, 7f, 8f })
            };
            var alloc = new TensorCachingAllocator();
            var applier = new DiscreteActionOutputApplier(new[] { 2, 3 }, 0, alloc);

            var agentIds = new List<int>() { 0, 1 };
            // Dictionary from AgentId to Action
            var actionDict = new Dictionary<int, float[]>() { { 0, null }, { 1, null } };


            applier.Apply(inputTensor, agentIds, actionDict);

            Assert.AreEqual(actionDict[0][0], 1);
            Assert.AreEqual(actionDict[0][1], 1);

            Assert.AreEqual(actionDict[1][0], 1);
            Assert.AreEqual(actionDict[1][1], 2);
            alloc.Dispose();
        }
    }
}
