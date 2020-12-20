# Reinforcement Learning on Unity Machine Learning Agents Toolkit

In this study, we will describe the mechanics of the game and its design and present the thought process of designing a reinforcement learning agent capable of playing this game. We explore scenarios on various difficulties and compare our agent's performance. Iteratively, we developed and evaluated each hypothesis; and recorded its results.

Our goal is to build a turret game where the player can shoot enemy units while allowing friendly units to enter. We will train a reinforcement learning agent to play as the turret, where its goal is to allow ten friendly units to enter the base, and loses if an enemy unit has entered the base or if two friendly units were shot.

## Source code

The source code can be found in the [ml-agents branch](https://github.com/jinglescode/unity-ml-agents-turret-defense/tree/ml-agents).

### Requirements

- Unity Platform - [Download](https://unity.com/)
- Unity Version: 2018.4.26f1
- [Unity ML-Agents Toolkit](https://github.com/Unity-Technologies/ml-agents)

## Citation

If you use our code in your project or research, or found this repo helpful, please cite our [paper](https://github.com/jinglescode/unity-ml-agents-turret-defense/blob/master/report.pdf)

```
@article{khok2020reinforcement,
  title={Reinforcement Learning on Unity Machine Learning Agents Toolkit},
  author={Khok, Hong Jing},
  journal={jinglescode.github.io},
  url={https://jinglescode.github.io/unity-ml-agents-turret-defense/},
  year={2020}
}
```

## Gameplay

We explore scenarios on various difficulties and compare our agent's performance.

#### Easy scenario

This scenario is where there are fixed spawn points for units, where the friendly and enemy spawn points are swapped at a random interval. Here we test the effects of delayed gratification where the agents are required to save ten friendly units from getting the reward. As this scenario is easy enough, the performance is astounding, with an almost 99% win rate. Out of 1000 episodes, there are five losses, all of which only happened in the first episode. 

This scenario is particularly important as it proves that the agent was able to distinguish between friendly and enemy units and was able to prioritize shooting nearer enemy units first. With these results, we are ready to challenge our agent to a slightly more difficult scenario.

Watch a snippet of the gameplay, might take awhile to load:

![easy](https://user-images.githubusercontent.com/1694368/95071905-f84a8e80-073c-11eb-96a4-b448744e7b19.gif)

#### Normal scenario

In this scenario, units are spawned randomly at a specified distance at a specified time interval. Instead of having the same movement speed for both friendly and enemy, we made the enemy faster. This gives a good challenge for our agent.

In most cases, where the agent failed, the enemy is behind a friendly unit, and as the enemy moves faster than the friendly unit, this would result in certain failure. We also notice that the rotation speed has limited the agent's performance, as turning towards an incoming unit from behind, especially enemy units that sneak behind a friendly unit; this would result in a lost episode. Also, in many cases, enemy units are too close to a friendly unit. As a result, shooting friendly units caused the agent to lose the match. One possible way to improve is to fine-tune the size of ray perception and the ray cast. 

Watch a snippet of the gameplay, might take awhile to load:

![norm](https://user-images.githubusercontent.com/1694368/95071927-026c8d00-073d-11eb-9cc7-866b45f8a15f.gif)

#### Advanced scenario

As in our design, enemy units are already moving faster than friendly units, with advanced enemy units, their movement speed is much more unpredictable. They could move forward faster and move left or right, hiding behind friendly units. This gives a great challenge for our agent; the winning rate is approximate 55\%. Like the normal scenario, there are many cases when the enemy units are hiding behind friendly units; this caused the enemy units to be too near before the agent had the chance to shoot it. At times, the enemy units were moving too close to friendly units. As a result, friendly fire happened, and the match was lost.

Watch a snippet of the gameplay, might take awhile to load:

![hard](https://user-images.githubusercontent.com/1694368/95071980-17492080-073d-11eb-9380-49bc7396f9af.gif)


#### Advanced scenario with power-ups

In this scenario, we are interested in seeing the effects on power-ups to see if it improves the agent's performance. The agent gained three times on rotation speed boost and no cooldown on firing by shooting on the power-up. Unfortunately, the performance did not improve. We hypothesize that the agent did not manage to associate shooting the power-ups with the rewards gain as we did not assign any rewards for hitting the power-ups. Therefore, we attempted to assign a small reward for shooting the power-ups. Unfortunately, we did not see any improvements as well.

Watch a snippet of the gameplay, might take awhile to load:

![hard-powerup](https://user-images.githubusercontent.com/1694368/95072010-2334e280-073d-11eb-9e6b-b257880106a1.gif)

