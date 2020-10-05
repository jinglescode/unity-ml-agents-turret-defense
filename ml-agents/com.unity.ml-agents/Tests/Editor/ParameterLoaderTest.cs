using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Unity.Barracuda;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Inference;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

namespace Unity.MLAgents.Tests
{
    public class Test3DSensorComponent : SensorComponent
    {
        public ISensor Sensor;

        public override ISensor CreateSensor()
        {
            return Sensor;
        }

        public override int[] GetObservationShape()
        {
            return Sensor.GetObservationShape();
        }
    }
    public class Test3DSensor : ISensor
    {
        int m_Width;
        int m_Height;
        int m_Channels;
        string m_Name;

        public Test3DSensor(string name, int width, int height, int channels)
        {
            m_Width = width;
            m_Height = height;
            m_Channels = channels;
            m_Name = name;
        }

        public int[] GetObservationShape()
        {
            return new[] { m_Height, m_Width, m_Channels };
        }

        public int Write(ObservationWriter writer)
        {
            for (int i = 0; i < m_Width * m_Height * m_Channels; i++)
            {
                writer[i] = 0.0f;
            }
            return m_Width * m_Height * m_Channels;
        }

        public byte[] GetCompressedObservation()
        {
            return new byte[0];
        }

        public void Update() { }
        public void Reset() { }

        public SensorCompressionType GetCompressionType()
        {
            return SensorCompressionType.None;
        }

        public string GetName()
        {
            return m_Name;
        }
    }

    [TestFixture]
    public class ParameterLoaderTest
    {
        const string k_continuous2vis8vec2actionPath = "Packages/com.unity.ml-agents/Tests/Editor/TestModels/continuous2vis8vec2action.nn";
        const string k_discrete1vis0vec_2_3action_recurrModelPath = "Packages/com.unity.ml-agents/Tests/Editor/TestModels/discrete1vis0vec_2_3action_recurr.nn";
        NNModel continuous2vis8vec2actionModel;
        NNModel discrete1vis0vec_2_3action_recurrModel;
        Test3DSensorComponent sensor_21_20_3;
        Test3DSensorComponent sensor_20_22_3;

        BrainParameters GetContinuous2vis8vec2actionBrainParameters()
        {
            var validBrainParameters = new BrainParameters();
            validBrainParameters.VectorObservationSize = 8;
            validBrainParameters.VectorActionSize = new[] { 2 };
            validBrainParameters.NumStackedVectorObservations = 1;
            validBrainParameters.VectorActionSpaceType = SpaceType.Continuous;
            return validBrainParameters;
        }

        BrainParameters GetDiscrete1vis0vec_2_3action_recurrModelBrainParameters()
        {
            var validBrainParameters = new BrainParameters();
            validBrainParameters.VectorObservationSize = 0;
            validBrainParameters.VectorActionSize = new[] { 2, 3 };
            validBrainParameters.NumStackedVectorObservations = 1;
            validBrainParameters.VectorActionSpaceType = SpaceType.Discrete;
            return validBrainParameters;
        }

        [SetUp]
        public void SetUp()
        {
            continuous2vis8vec2actionModel = (NNModel)AssetDatabase.LoadAssetAtPath(k_continuous2vis8vec2actionPath, typeof(NNModel));
            discrete1vis0vec_2_3action_recurrModel = (NNModel)AssetDatabase.LoadAssetAtPath(k_discrete1vis0vec_2_3action_recurrModelPath, typeof(NNModel));
            var go = new GameObject("SensorA");
            sensor_21_20_3 = go.AddComponent<Test3DSensorComponent>();
            sensor_21_20_3.Sensor = new Test3DSensor("SensorA", 21, 20, 3);
            sensor_20_22_3 = go.AddComponent<Test3DSensorComponent>();
            sensor_20_22_3.Sensor = new Test3DSensor("SensorA", 20, 22, 3);
        }

        [Test]
        public void TestModelExist()
        {
            Assert.IsNotNull(continuous2vis8vec2actionModel);
            Assert.IsNotNull(discrete1vis0vec_2_3action_recurrModel);
        }

        [Test]
        public void TestGetInputTensors1()
        {
            var model = ModelLoader.Load(continuous2vis8vec2actionModel);
            var inputTensors = BarracudaModelParamLoader.GetInputTensors(model);
            var inputNames = inputTensors.Select(x => x.name).ToList();
            // Model should contain 3 inputs : vector, visual 1 and visual 2
            Assert.AreEqual(3, inputNames.Count);
            Assert.Contains(TensorNames.VectorObservationPlaceholder, inputNames);
            Assert.Contains(TensorNames.VisualObservationPlaceholderPrefix + "0", inputNames);
            Assert.Contains(TensorNames.VisualObservationPlaceholderPrefix + "1", inputNames);

            Assert.AreEqual(2, BarracudaModelParamLoader.GetNumVisualInputs(model));

            // Test if the model is null
            Assert.AreEqual(0, BarracudaModelParamLoader.GetInputTensors(null).Count);
            Assert.AreEqual(0, BarracudaModelParamLoader.GetNumVisualInputs(null));
        }

        [Test]
        public void TestGetInputTensors2()
        {
            var model = ModelLoader.Load(discrete1vis0vec_2_3action_recurrModel);
            var inputTensors = BarracudaModelParamLoader.GetInputTensors(model);
            var inputNames = inputTensors.Select(x => x.name).ToList();
            // Model should contain 2 inputs : recurrent and visual 1

            Assert.Contains(TensorNames.VisualObservationPlaceholderPrefix + "0", inputNames);
            // TODO :There are some memory tensors as well
        }

        [Test]
        public void TestGetOutputTensors1()
        {
            var model = ModelLoader.Load(continuous2vis8vec2actionModel);
            var outputNames = BarracudaModelParamLoader.GetOutputNames(model);
            Assert.Contains(TensorNames.ActionOutput, outputNames);
            Assert.AreEqual(1, outputNames.Count());

            Assert.AreEqual(0, BarracudaModelParamLoader.GetOutputNames(null).Count());
        }

        [Test]
        public void TestGetOutputTensors2()
        {
            var model = ModelLoader.Load(discrete1vis0vec_2_3action_recurrModel);
            var outputNames = BarracudaModelParamLoader.GetOutputNames(model);
            Assert.Contains(TensorNames.ActionOutput, outputNames);
            // TODO : There are some memory tensors as well
        }

        [Test]
        public void TestCheckModelValid1()
        {
            var model = ModelLoader.Load(continuous2vis8vec2actionModel);
            var validBrainParameters = GetContinuous2vis8vec2actionBrainParameters();

            var errors = BarracudaModelParamLoader.CheckModel(
                model, validBrainParameters,
                new SensorComponent[] { sensor_21_20_3, sensor_20_22_3 }, new ActuatorComponent[0]
            );
            Assert.AreEqual(0, errors.Count()); // There should not be any errors
        }

        [Test]
        public void TestCheckModelValid2()
        {
            var model = ModelLoader.Load(discrete1vis0vec_2_3action_recurrModel);
            var validBrainParameters = GetDiscrete1vis0vec_2_3action_recurrModelBrainParameters();

            var errors = BarracudaModelParamLoader.CheckModel(
                model, validBrainParameters,
                new SensorComponent[] { sensor_21_20_3 }, new ActuatorComponent[0]
            );
            Assert.AreEqual(0, errors.Count()); // There should not be any errors
        }

        [Test]
        public void TestCheckModelThrowsVectorObservation1()
        {
            var model = ModelLoader.Load(continuous2vis8vec2actionModel);

            var brainParameters = GetContinuous2vis8vec2actionBrainParameters();
            brainParameters.VectorObservationSize = 9; // Invalid observation
            var errors = BarracudaModelParamLoader.CheckModel(
                model, brainParameters,
                new SensorComponent[] { sensor_21_20_3, sensor_20_22_3 }, new ActuatorComponent[0]
            );
            Assert.Greater(errors.Count(), 0);

            brainParameters = GetContinuous2vis8vec2actionBrainParameters();
            brainParameters.NumStackedVectorObservations = 2;// Invalid stacking
            errors = BarracudaModelParamLoader.CheckModel(
                model, brainParameters,
                new SensorComponent[] { sensor_21_20_3, sensor_20_22_3 }, new ActuatorComponent[0]
            );
            Assert.Greater(errors.Count(), 0);
        }

        [Test]
        public void TestCheckModelThrowsVectorObservation2()
        {
            var model = ModelLoader.Load(discrete1vis0vec_2_3action_recurrModel);

            var brainParameters = GetDiscrete1vis0vec_2_3action_recurrModelBrainParameters();
            brainParameters.VectorObservationSize = 1; // Invalid observation
            var errors = BarracudaModelParamLoader.CheckModel(model, brainParameters, new SensorComponent[] { sensor_21_20_3 }, new ActuatorComponent[0]);
            Assert.Greater(errors.Count(), 0);
        }

        [Test]
        public void TestCheckModelThrowsAction1()
        {
            var model = ModelLoader.Load(continuous2vis8vec2actionModel);

            var brainParameters = GetContinuous2vis8vec2actionBrainParameters();
            brainParameters.VectorActionSize = new[] { 3 }; // Invalid action
            var errors = BarracudaModelParamLoader.CheckModel(model, brainParameters, new SensorComponent[] { sensor_21_20_3, sensor_20_22_3 }, new ActuatorComponent[0]);
            Assert.Greater(errors.Count(), 0);

            brainParameters = GetContinuous2vis8vec2actionBrainParameters();
            brainParameters.VectorActionSpaceType = SpaceType.Discrete;// Invalid SpaceType
            errors = BarracudaModelParamLoader.CheckModel(model, brainParameters, new SensorComponent[] { sensor_21_20_3, sensor_20_22_3 }, new ActuatorComponent[0]);
            Assert.Greater(errors.Count(), 0);
        }

        [Test]
        public void TestCheckModelThrowsAction2()
        {
            var model = ModelLoader.Load(discrete1vis0vec_2_3action_recurrModel);

            var brainParameters = GetDiscrete1vis0vec_2_3action_recurrModelBrainParameters();
            brainParameters.VectorActionSize = new[] { 3, 3 }; // Invalid action
            var errors = BarracudaModelParamLoader.CheckModel(model, brainParameters, new SensorComponent[] { sensor_21_20_3 }, new ActuatorComponent[0]);
            Assert.Greater(errors.Count(), 0);

            brainParameters = GetContinuous2vis8vec2actionBrainParameters();
            brainParameters.VectorActionSpaceType = SpaceType.Continuous;// Invalid SpaceType
            errors = BarracudaModelParamLoader.CheckModel(model, brainParameters, new SensorComponent[] { sensor_21_20_3 }, new ActuatorComponent[0]);
            Assert.Greater(errors.Count(), 0);
        }

        [Test]
        public void TestCheckModelThrowsNoModel()
        {
            var brainParameters = GetContinuous2vis8vec2actionBrainParameters();
            var errors = BarracudaModelParamLoader.CheckModel(null, brainParameters, new SensorComponent[] { sensor_21_20_3, sensor_20_22_3 }, new ActuatorComponent[0]);
            Assert.Greater(errors.Count(), 0);
        }
    }
}
