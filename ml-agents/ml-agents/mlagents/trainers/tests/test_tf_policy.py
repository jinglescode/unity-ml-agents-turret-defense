from mlagents.trainers.policy.tf_policy import TFPolicy
from mlagents_envs.base_env import DecisionSteps, BehaviorSpec
from mlagents.trainers.action_info import ActionInfo
from unittest.mock import MagicMock
from mlagents.trainers.settings import TrainerSettings
import numpy as np


def basic_mock_brain():
    mock_brain = MagicMock()
    mock_brain.vector_action_space_type = "continuous"
    mock_brain.vector_observation_space_size = 1
    mock_brain.vector_action_space_size = [1]
    mock_brain.brain_name = "MockBrain"
    return mock_brain


class FakePolicy(TFPolicy):
    def create_tf_graph(self):
        pass

    def get_trainable_variables(self):
        return []


def test_take_action_returns_empty_with_no_agents():
    test_seed = 3
    policy = FakePolicy(test_seed, basic_mock_brain(), TrainerSettings(), "output")
    # Doesn't really matter what this is
    dummy_groupspec = BehaviorSpec([(1,)], "continuous", 1)
    no_agent_step = DecisionSteps.empty(dummy_groupspec)
    result = policy.get_action(no_agent_step)
    assert result == ActionInfo.empty()


def test_take_action_returns_nones_on_missing_values():
    test_seed = 3
    policy = FakePolicy(test_seed, basic_mock_brain(), TrainerSettings(), "output")
    policy.evaluate = MagicMock(return_value={})
    policy.save_memories = MagicMock()
    step_with_agents = DecisionSteps(
        [], np.array([], dtype=np.float32), np.array([0]), None
    )
    result = policy.get_action(step_with_agents, worker_id=0)
    assert result == ActionInfo(None, None, {}, [0])


def test_take_action_returns_action_info_when_available():
    test_seed = 3
    policy = FakePolicy(test_seed, basic_mock_brain(), TrainerSettings(), "output")
    policy_eval_out = {
        "action": np.array([1.0], dtype=np.float32),
        "memory_out": np.array([[2.5]], dtype=np.float32),
        "value": np.array([1.1], dtype=np.float32),
    }
    policy.evaluate = MagicMock(return_value=policy_eval_out)
    step_with_agents = DecisionSteps(
        [], np.array([], dtype=np.float32), np.array([0]), None
    )
    result = policy.get_action(step_with_agents)
    expected = ActionInfo(
        policy_eval_out["action"], policy_eval_out["value"], policy_eval_out, [0]
    )
    assert result == expected


def test_convert_version_string():
    result = TFPolicy._convert_version_string("200.300.100")
    assert result == (200, 300, 100)
    # Test dev versions
    result = TFPolicy._convert_version_string("200.300.100.dev0")
    assert result == (200, 300, 100)
