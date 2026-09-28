# Agent

The boss's brain (T6). ML-Agents picks one `BossMove` per decision and `BossBody` performs it. How moves look, hit and
time out lives in Boss; this folder only decides.

## What is here

- `Scripts/BossAgent.cs`: the Agent. Observations, action mask, rewards, episode loop.
- `Prefabs/BossAgent.prefab`: Boss variant with Behavior Parameters (name `Boss`, 46 observations, one branch of 10),
  a Decision Requester (every 5 physics steps, so 0.1 s) and the `BossAgent`.
- `Prefabs/TrainingArena.prefab`: ArenaContainer variant. The BossAgent is fighter 2, a sparring dummy is fighter 1,
  8 m apart, 60 s rounds, Auto Restart off (the agent starts every fight).
- `Scenes/Agent_Boss.unity`: one training arena. Play the boss by hand through the agent (Behavior Type is
  Heuristic Only in this scene). `Scripts/BossAgentHud.cs` is the overlay.
- `Scenes/Agent_Smoke.unity`, `Scripts/SmokeAgent.cs`: the pipeline smoke test from T1. Not part of the game.

## Actions

One discrete branch with one entry per `BossMove`: None, Advance, Retreat, StrafeLeft, StrafeRight, QuickAttack,
HeavySlam, SuperAttack, RangedShot, AoeBurst. Every decision masks what `BossBody.CanPerform` says no to. None is never
masked, so there is always a legal choice.

## Observations (v0, 46 floats, all scaled to about -1..1)

| Group | Values |
|---|---|
| Boss (27) | health, state one-hot (4), current attack one-hot (6), attack phase one-hot (4), progress through the phase, cooldown left per attack (5), stun left, locomotion one-hot (5) |
| Opponent position (7) | distance, direction to it in the boss's frame (2), its facing in the boss's frame (2), its velocity in the boss's frame (2) |
| Opponent state (9) | health, stamina, attack phase one-hot (4), progress through the phase, damage of its current attack, invulnerable (rolling) |
| Arena (3) | boss position from the arena center, sideways and along the line to the opponent (2), round time left |

T10's full list replaces this in Phase 3. Changing the observations means retraining from scratch.

## Rewards

| Event | Default | Trainer parameter |
|---|---|---|
| Win | +1 | `win_reward` |
| Loss | -1 | `loss_penalty` |
| Damage dealt | +0.5 for a whole health bar, in proportion | `damage_dealt_reward` |
| Damage taken | -0.5 for a whole health bar, in proportion (stun hits count double) | `damage_taken_penalty` |
| Fighting time | -0.002 per second | `time_penalty_per_second` |

A draw (time runs out) adds nothing extra. Set any of these under `environment_parameters` in the trainer config to try
other weights without a new build.

## Episode loop

When the arena's fight ends, the agent adds the win or loss reward and ends its episode on the next physics step. The
next episode starts a new fight (both fighters back on their spawn points at full health) and resets the boss:
cooldowns, stun, and shots still in flight. TensorBoard gets `Fight/BossWinRate`, `Fight/DrawRate` and `Fight/Length`.

## Playing the boss by hand

Open `Scenes/Agent_Boss.unity` and press Play. WASD toward the dummy is Advance, away is Retreat, sideways strafes.
J or left click is Quick, K or right click is Slam, 1 is Super, 2 is Ranged, 3 is AoE. The dummy swings a heavy attack
every 2.5 s at whatever stands in front of it. The overlay shows the round clock, both healths, the move the agent
took, which moves are allowed and the reward so far.
