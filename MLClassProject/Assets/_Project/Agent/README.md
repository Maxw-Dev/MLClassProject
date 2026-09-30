# Agent

The boss's brain (T6). ML-Agents picks one `BossMove` per decision and `BossBody` performs it. How moves look, hit and
time out lives in Boss; this folder only decides.

## What is here

- `Scripts/BossAgent.cs`: the Agent. Observations, action mask, rewards, episode loop.
- `Prefabs/BossAgent.prefab`: Boss variant with Behavior Parameters (name `Boss`, 46 observations, one branch of 10),
  a Decision Requester (every 5 physics steps, so 0.1 s) and the `BossAgent`.
- `Prefabs/TrainingArena.prefab`: ArenaContainer variant. The BossAgent is fighter 2 and the PlayerBot (T12) is
  fighter 1, wired to each other rather than found by tag, so many arenas can run side by side. 8 m apart,
  60 s rounds, Auto Restart off (the agent starts every fight).
- `Scenes/Agent_Boss.unity`: one training arena where the trained boss (Inference Only, `Models/Boss_v0_609k.onnx`)
  fights the PlayerBot, to watch how a model plays. Set its Behavior Type to Heuristic Only to steer the boss yourself.
  `Scripts/BossAgentHud.cs` is the overlay.
- `Scenes/Agent_Train.unity`: eight training arenas, 80 m apart so a boss shot (20 m) never reaches the next one.
  This is the scene the training build profiles build. `Scripts/TrainingLogFilter.cs` hides info-level logs there.
- `Scenes/Agent_Play.unity`: you play the player (keyboard) against a trained boss. The boss runs a model from
  `Models/` with Behavior Type Inference Only.
- `Models/`: trained boss models copied in from `results/`. `Boss_v0_609k.onnx` is the first run, 609k steps against
  the default bot, trained on the first-pass boss frame data. Retrain after changing move timings in `Boss/Data`:
  the model learned the old ones.
- `config/boss.yaml` (repo root): the PPO settings.
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
| Win | +1, down to +0.5 for a win at the buzzer | `win_reward`, `win_time_decay` |
| Loss | -1, however long the fight took | `loss_penalty` |
| Draw (time runs out) | -0.5 | `draw_penalty` |
| Damage dealt | +0.5 for a whole health bar, in proportion. Overkill does not count | `damage_dealt_reward` |
| Damage taken | -0.5 for a whole health bar, in proportion. Hits while stunned count double | `damage_taken_penalty` |
| Fighting time | off | `time_penalty_per_second` |

What a fight is worth with these defaults (60 s round):

| Outcome | Reward |
|---|---|
| Fast clean win (15 s) | +1.38 |
| Slow win (55 s, lost half its health) | +0.79 |
| Draw, boss ahead (dealt 60%, took 20%) | -0.30 |
| Draw, nobody hit | -0.50 |
| Close loss (dealt 90%) | -1.05 |
| Blowout loss | -1.50 |

Why these numbers, from the ML-Agents reward guidance and Unity's example games:

- Every win beats every draw and every draw beats every loss, so running out the clock never pays.
- Time pressure sits on the win (Unity's Soccer example does the same), not on every second. A per-second penalty also
  makes a slow loss cost more than a fast one, which teaches a losing boss to give up.
- Rewards per decision stay within about -1..1, and most of the signal is positive (damage dealt, winning).
- Health bars are compared as a whole: the boss has 300 health and the player 100, so each point the boss deals is
  worth three it takes and trading hits pays. Raise `damage_taken_penalty` if it plays too recklessly.

### Tuning

1. Decide the order the outcomes should come in (the table above), then play each one by hand in `Agent_Boss` (Behavior Type set to Heuristic Only): a fast
   win, running away for 60 s, taking a lead and then running. The overlay's episode reward should come out in that order.
2. Change one weight per training run under `environment_parameters` in the trainer config. No new build needed:

   ```yaml
   environment_parameters:
     draw_penalty: 0.75
   ```

3. Compare runs on `Fight/BossWinRate`, `Fight/DrawRate` and `Fight/Length`. Cumulative Reward changes scale whenever a
   weight changes, so it cannot be compared across runs.
4. Fights are about 600 decisions long, so the trainer config should look further ahead than the defaults:
   `gamma: 0.995` and `time_horizon: 512`.

## Episode loop

When the arena's fight ends, the agent adds the win, draw or loss reward and ends its episode on the next physics step. The
next episode starts a new fight (both fighters back on their spawn points at full health) and resets the boss:
cooldowns, stun, and shots still in flight. It also calls the opponent's `PlayerBody.Reset()`, so a player that died
comes back. TensorBoard gets `Fight/BossWinRate`, `Fight/DrawRate` and `Fight/Length`.

## Watching the boss, or steering it by hand

Open `Scenes/Agent_Boss.unity` and press Play to watch the trained boss fight the PlayerBot. To steer the boss
yourself instead, select TrainingArena > Boss and set Behavior Parameters > Behavior Type to Heuristic Only (the
Keyboard object is already wired to it). Then WASD toward the bot is Advance, away is Retreat, sideways strafes.
J or left click is Quick, K or right click is Slam, 1 is Super, 2 is Ranged, 3 is AoE. The PlayerBot fights back: it
walks in and attacks while the boss is idle, backs off from most attacks, rolls away from the ranged shot and punishes
the super. The overlay shows the round clock, both healths, the move the agent took, which moves are allowed and the
reward so far.

## Playing against a trained boss

Open `Scenes/Agent_Play.unity` and press Play, with no trainer running. WASD moves, J or left click is the light
attack, K or right click the heavy, Space rolls. A new round starts as soon as one ends, and the overlay shows the
round clock, both healths and what the boss picks.

To try another model, copy its `.onnx` from `results/<run-id>/` (or a checkpoint from `results/<run-id>/Boss/`) into
`Models/`, select TrainingArena > Boss in the scene, and set Behavior Parameters > Model to it.

## Training

In the editor, from the repo root:

```
uv run mlagents-learn config/boss.yaml --run-id=boss_v0
```

then open `Scenes/Agent_Train.unity` and press Play once the trainer says it is listening (it waits about a minute).
Run the same command with `--resume` to continue a run, or with `--force` to start it over. A run that never got
going leaves a `results/<run-id>` folder behind, and the next start with that id stops until you add one of them.

From a build: File > Build Profiles, pick the macOS or Windows profile (both build `Agent_Train`), build it into
`builds/` at the repo root, then:

```
./tools/train.sh config/boss.yaml boss_v0 builds/MLClassProject.app 4 20 --no-graphics
```

Watch with `uv run tensorboard --logdir results`. A run is learning when `Fight/BossWinRate` climbs, `Fight/DrawRate`
stays low and `Environment/Cumulative Reward` rises. The model lands in `results/<run-id>/Boss.onnx`.

The PlayerBot decides once per rendered frame, so at 20x speed it reacts about three times per game second instead of
sixty. Until it decides in FixedUpdate (T12), the boss is learning against a slow bot.
