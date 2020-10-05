import pytest

from mlagents.torch_utils import torch
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents.trainers.tests import mock_brain as mb
from mlagents.trainers.settings import TrainerSettings, NetworkSettings
from mlagents.trainers.torch.utils import ModelUtils

VECTOR_ACTION_SPACE = 2
VECTOR_OBS_SPACE = 8
DISCRETE_ACTION_SPACE = [3, 3, 3, 2]
BUFFER_INIT_SAMPLES = 32
NUM_AGENTS = 12
EPSILON = 1e-7


def create_policy_mock(
    dummy_config: TrainerSettings,
    use_rnn: bool = False,
    use_discrete: bool = True,
    use_visual: bool = False,
    seed: int = 0,
) -> TorchPolicy:
    mock_spec = mb.setup_test_behavior_specs(
        use_discrete,
        use_visual,
        vector_action_space=DISCRETE_ACTION_SPACE
        if use_discrete
        else VECTOR_ACTION_SPACE,
        vector_obs_space=VECTOR_OBS_SPACE,
    )

    trainer_settings = dummy_config
    trainer_settings.keep_checkpoints = 3
    trainer_settings.network_settings.memory = (
        NetworkSettings.MemorySettings() if use_rnn else None
    )
    policy = TorchPolicy(seed, mock_spec, trainer_settings)
    return policy


@pytest.mark.parametrize("discrete", [True, False], ids=["discrete", "continuous"])
@pytest.mark.parametrize("visual", [True, False], ids=["visual", "vector"])
@pytest.mark.parametrize("rnn", [True, False], ids=["rnn", "no_rnn"])
def test_policy_evaluate(rnn, visual, discrete):
    # Test evaluate
    policy = create_policy_mock(
        TrainerSettings(), use_rnn=rnn, use_discrete=discrete, use_visual=visual
    )
    decision_step, terminal_step = mb.create_steps_from_behavior_spec(
        policy.behavior_spec, num_agents=NUM_AGENTS
    )

    run_out = policy.evaluate(decision_step, list(decision_step.agent_id))
    if discrete:
        run_out["action"].shape == (NUM_AGENTS, len(DISCRETE_ACTION_SPACE))
    else:
        assert run_out["action"].shape == (NUM_AGENTS, VECTOR_ACTION_SPACE)


@pytest.mark.parametrize("discrete", [True, False], ids=["discrete", "continuous"])
@pytest.mark.parametrize("visual", [True, False], ids=["visual", "vector"])
@pytest.mark.parametrize("rnn", [True, False], ids=["rnn", "no_rnn"])
def test_evaluate_actions(rnn, visual, discrete):
    policy = create_policy_mock(
        TrainerSettings(), use_rnn=rnn, use_discrete=discrete, use_visual=visual
    )
    buffer = mb.simulate_rollout(64, policy.behavior_spec, memory_size=policy.m_size)
    vec_obs = [ModelUtils.list_to_tensor(buffer["vector_obs"])]
    act_masks = ModelUtils.list_to_tensor(buffer["action_mask"])
    if policy.use_continuous_act:
        actions = ModelUtils.list_to_tensor(buffer["actions"]).unsqueeze(-1)
    else:
        actions = ModelUtils.list_to_tensor(buffer["actions"], dtype=torch.long)
    vis_obs = []
    for idx, _ in enumerate(policy.actor_critic.network_body.visual_processors):
        vis_ob = ModelUtils.list_to_tensor(buffer["visual_obs%d" % idx])
        vis_obs.append(vis_ob)

    memories = [
        ModelUtils.list_to_tensor(buffer["memory"][i])
        for i in range(0, len(buffer["memory"]), policy.sequence_length)
    ]
    if len(memories) > 0:
        memories = torch.stack(memories).unsqueeze(0)

    log_probs, entropy, values = policy.evaluate_actions(
        vec_obs,
        vis_obs,
        masks=act_masks,
        actions=actions,
        memories=memories,
        seq_len=policy.sequence_length,
    )
    assert log_probs.shape == (64, policy.behavior_spec.action_size)
    assert entropy.shape == (64, policy.behavior_spec.action_size)
    for val in values.values():
        assert val.shape == (64,)


@pytest.mark.parametrize("discrete", [True, False], ids=["discrete", "continuous"])
@pytest.mark.parametrize("visual", [True, False], ids=["visual", "vector"])
@pytest.mark.parametrize("rnn", [True, False], ids=["rnn", "no_rnn"])
def test_sample_actions(rnn, visual, discrete):
    policy = create_policy_mock(
        TrainerSettings(), use_rnn=rnn, use_discrete=discrete, use_visual=visual
    )
    buffer = mb.simulate_rollout(64, policy.behavior_spec, memory_size=policy.m_size)
    vec_obs = [ModelUtils.list_to_tensor(buffer["vector_obs"])]
    act_masks = ModelUtils.list_to_tensor(buffer["action_mask"])

    vis_obs = []
    for idx, _ in enumerate(policy.actor_critic.network_body.visual_processors):
        vis_ob = ModelUtils.list_to_tensor(buffer["visual_obs%d" % idx])
        vis_obs.append(vis_ob)

    memories = [
        ModelUtils.list_to_tensor(buffer["memory"][i])
        for i in range(0, len(buffer["memory"]), policy.sequence_length)
    ]
    if len(memories) > 0:
        memories = torch.stack(memories).unsqueeze(0)

    (
        sampled_actions,
        log_probs,
        entropies,
        sampled_values,
        memories,
    ) = policy.sample_actions(
        vec_obs,
        vis_obs,
        masks=act_masks,
        memories=memories,
        seq_len=policy.sequence_length,
        all_log_probs=not policy.use_continuous_act,
    )
    if discrete:
        assert log_probs.shape == (
            64,
            sum(policy.behavior_spec.discrete_action_branches),
        )
    else:
        assert log_probs.shape == (64, policy.behavior_spec.action_shape)
    assert entropies.shape == (64, policy.behavior_spec.action_size)
    for val in sampled_values.values():
        assert val.shape == (64,)

    if rnn:
        assert memories.shape == (1, 1, policy.m_size)
