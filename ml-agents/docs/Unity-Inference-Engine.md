# Unity Inference Engine

The ML-Agents Toolkit allows you to use pre-trained neural network models inside
your Unity games. This support is possible thanks to the
[Unity Inference Engine](https://docs.unity3d.com/Packages/com.unity.barracuda@latest/index.html)
(codenamed Barracuda). The Unity Inference Engine uses
[compute shaders](https://docs.unity3d.com/Manual/class-ComputeShader.html) to
run the neural network within Unity.

## Supported devices

See the Unity Inference Engine documentation for a list of the
[supported platforms](https://docs.unity3d.com/Packages/com.unity.barracuda@latest/index.html#supported-platforms).

Scripting Backends : The Unity Inference Engine is generally faster with
**IL2CPP** than with **Mono** for Standalone builds. In the Editor, It is not
possible to use the Unity Inference Engine with GPU device selected when Editor
Graphics Emulation is set to **OpenGL(ES) 3.0 or 2.0 emulation**. Also there
might be non-fatal build time errors when target platform includes Graphics API
that does not support **Unity Compute Shaders**.

## Supported formats

There are currently two supported model formats:

- Barracuda (`.nn`) files use a proprietary format produced by the
  [`tensorflow_to_barracuda.py`]() script.
- ONNX (`.onnx`) files use an
  [industry-standard open format](https://onnx.ai/about.html) produced by the
  [tf2onnx package](https://github.com/onnx/tensorflow-onnx).

Export to ONNX is currently considered beta. To enable it, make sure
`tf2onnx>=1.5.5` is installed in pip. tf2onnx does not currently support
tensorflow 2.0.0 or later, or earlier than 1.12.0.

## Using the Unity Inference Engine

When using a model, drag the model file into the **Model** field in the
Inspector of the Agent. Select the **Inference Device** : CPU or GPU you want to
use for Inference.

**Note:** For most of the models generated with the ML-Agents Toolkit, CPU will
be faster than GPU. You should use the GPU only if you use the ResNet visual
encoder or have a large number of agents with visual observations.

# Unsupported use cases
## Externally trained models
The ML-Agents Toolkit only supports the models created with our trainers. Model
loading expects certain conventions for constants and tensor names. While it is
possible to construct a model that follows these conventions, we don't provide
any additional help for this. More details can be found in
[TensorNames.cs](https://github.com/Unity-Technologies/ml-agents/blob/release_6_docs/com.unity.ml-agents/Runtime/Inference/TensorNames.cs)
and
[BarracudaModelParamLoader.cs](https://github.com/Unity-Technologies/ml-agents/blob/release_6_docs/com.unity.ml-agents/Runtime/Inference/BarracudaModelParamLoader.cs).

If you wish to run inference on an externally trained model, you should use
Barracuda directly, instead of trying to run it through ML-Agents.

## Model inference outside of Unity
We do not provide support for inference anywhere outside of Unity. The
`frozen_graph_def.pb` and `.onnx` files produced by training are open formats
for TensorFlow and ONNX respectively; if you wish to convert these to another
format or run inference with them, refer to their documentation.
