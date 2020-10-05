import abc
from typing import List
from mlagents.torch_utils import torch, nn
import numpy as np
import math
from mlagents.trainers.torch.layers import linear_layer, Initialization

EPSILON = 1e-7  # Small value to avoid divide by zero


class DistInstance(nn.Module, abc.ABC):
    @abc.abstractmethod
    def sample(self) -> torch.Tensor:
        """
        Return a sample from this distribution.
        """
        pass

    @abc.abstractmethod
    def log_prob(self, value: torch.Tensor) -> torch.Tensor:
        """
        Returns the log probabilities of a particular value.
        :param value: A value sampled from the distribution.
        :returns: Log probabilities of the given value.
        """
        pass

    @abc.abstractmethod
    def entropy(self) -> torch.Tensor:
        """
        Returns the entropy of this distribution.
        """
        pass


class DiscreteDistInstance(DistInstance):
    @abc.abstractmethod
    def all_log_prob(self) -> torch.Tensor:
        """
        Returns the log probabilities of all actions represented by this distribution.
        """
        pass


class GaussianDistInstance(DistInstance):
    def __init__(self, mean, std):
        super().__init__()
        self.mean = mean
        self.std = std

    def sample(self):
        sample = self.mean + torch.randn_like(self.mean) * self.std
        return sample

    def log_prob(self, value):
        var = self.std ** 2
        log_scale = torch.log(self.std + EPSILON)
        return (
            -((value - self.mean) ** 2) / (2 * var + EPSILON)
            - log_scale
            - math.log(math.sqrt(2 * math.pi))
        )

    def pdf(self, value):
        log_prob = self.log_prob(value)
        return torch.exp(log_prob)

    def entropy(self):
        return 0.5 * torch.log(2 * math.pi * math.e * self.std + EPSILON)


class TanhGaussianDistInstance(GaussianDistInstance):
    def __init__(self, mean, std):
        super().__init__(mean, std)
        self.transform = torch.distributions.transforms.TanhTransform(cache_size=1)

    def sample(self):
        unsquashed_sample = super().sample()
        squashed = self.transform(unsquashed_sample)
        return squashed

    def _inverse_tanh(self, value):
        capped_value = torch.clamp(value, -1 + EPSILON, 1 - EPSILON)
        return 0.5 * torch.log((1 + capped_value) / (1 - capped_value) + EPSILON)

    def log_prob(self, value):
        unsquashed = self.transform.inv(value)
        return super().log_prob(unsquashed) - self.transform.log_abs_det_jacobian(
            unsquashed, value
        )


class CategoricalDistInstance(DiscreteDistInstance):
    def __init__(self, logits):
        super().__init__()
        self.logits = logits
        self.probs = torch.softmax(self.logits, dim=-1)

    def sample(self):
        return torch.multinomial(self.probs, 1)

    def pdf(self, value):
        # This function is equivalent to torch.diag(self.probs.T[value.flatten().long()]),
        # but torch.diag is not supported by ONNX export.
        idx = torch.arange(start=0, end=len(value)).unsqueeze(-1)
        return torch.gather(
            self.probs.permute(1, 0)[value.flatten().long()], -1, idx
        ).squeeze(-1)

    def log_prob(self, value):
        return torch.log(self.pdf(value))

    def all_log_prob(self):
        return torch.log(self.probs)

    def entropy(self):
        return -torch.sum(self.probs * torch.log(self.probs), dim=-1)


class GaussianDistribution(nn.Module):
    def __init__(
        self,
        hidden_size: int,
        num_outputs: int,
        conditional_sigma: bool = False,
        tanh_squash: bool = False,
    ):
        super().__init__()
        self.conditional_sigma = conditional_sigma
        self.mu = linear_layer(
            hidden_size,
            num_outputs,
            kernel_init=Initialization.KaimingHeNormal,
            kernel_gain=0.1,
            bias_init=Initialization.Zero,
        )
        self.tanh_squash = tanh_squash
        if conditional_sigma:
            self.log_sigma = linear_layer(
                hidden_size,
                num_outputs,
                kernel_init=Initialization.KaimingHeNormal,
                kernel_gain=0.1,
                bias_init=Initialization.Zero,
            )
        else:
            self.log_sigma = nn.Parameter(
                torch.zeros(1, num_outputs, requires_grad=True)
            )

    def forward(self, inputs: torch.Tensor) -> List[DistInstance]:
        mu = self.mu(inputs)
        if self.conditional_sigma:
            log_sigma = torch.clamp(self.log_sigma(inputs), min=-20, max=2)
        else:
            log_sigma = self.log_sigma
        if self.tanh_squash:
            return [TanhGaussianDistInstance(mu, torch.exp(log_sigma))]
        else:
            return [GaussianDistInstance(mu, torch.exp(log_sigma))]


class MultiCategoricalDistribution(nn.Module):
    def __init__(self, hidden_size: int, act_sizes: List[int]):
        super().__init__()
        self.act_sizes = act_sizes
        self.branches = self._create_policy_branches(hidden_size)

    def _create_policy_branches(self, hidden_size: int) -> nn.ModuleList:
        branches = []
        for size in self.act_sizes:
            branch_output_layer = linear_layer(
                hidden_size,
                size,
                kernel_init=Initialization.KaimingHeNormal,
                kernel_gain=0.1,
                bias_init=Initialization.Zero,
            )
            branches.append(branch_output_layer)
        return nn.ModuleList(branches)

    def _mask_branch(self, logits: torch.Tensor, mask: torch.Tensor) -> torch.Tensor:
        raw_probs = torch.nn.functional.softmax(logits, dim=-1) * mask
        normalized_probs = raw_probs / torch.sum(raw_probs, dim=-1).unsqueeze(-1)
        normalized_logits = torch.log(normalized_probs + EPSILON)
        return normalized_logits

    def _split_masks(self, masks: torch.Tensor) -> List[torch.Tensor]:
        split_masks = []
        for idx, _ in enumerate(self.act_sizes):
            start = int(np.sum(self.act_sizes[:idx]))
            end = int(np.sum(self.act_sizes[: idx + 1]))
            split_masks.append(masks[:, start:end])
        return split_masks

    def forward(self, inputs: torch.Tensor, masks: torch.Tensor) -> List[DistInstance]:
        # Todo - Support multiple branches in mask code
        branch_distributions = []
        masks = self._split_masks(masks)
        for idx, branch in enumerate(self.branches):
            logits = branch(inputs)
            norm_logits = self._mask_branch(logits, masks[idx])
            distribution = CategoricalDistInstance(norm_logits)
            branch_distributions.append(distribution)
        return branch_distributions
