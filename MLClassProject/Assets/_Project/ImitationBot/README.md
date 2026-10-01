# ImitationBot

A player bot that plays like a real person (T18). We record ourselves fighting the trained boss, then ML-Agents
learns from those recordings (behavioral cloning and GAIL), still against the trained boss. The result is one more
opponent for boss training, and a way to compare how each of us plays.

## What is here

- `Scripts/PlayerAgent.cs`: the player's side as an ML-Agents Agent. Behavior name `Player`.
  - With Human Input set, a person drives the player as usual and the agent records what they do.
  - With it empty, the agent is the player's input and drives it.
  - It never starts fights. It waits for the arena (Auto Restart on) or the BossAgent (training), and makes no
    decisions between fights, so recordings only hold fighting.
- `Scripts/PlayerActions.cs`: how actions become an `Intent` and back. Tested in `Tests/`.
- `Scripts/DelayLine.cs`: keeps the last half second of boss sightings, for the reaction delay. Tested in `Tests/`.
- `Scenes/Imitation_Record.unity`: `Agent_Play` (you vs Boss_v2) with the agent and a Demonstration Recorder on the
  player.
- `Prefabs/PlayerAgent.prefab`: Player variant for training and playing the bot. The keyboard input is removed and
  Behavior Parameters (`Player`, Behavior Type Default) and `PlayerAgent` are added.
- `Scenes/Imitation_Train.unity`: `Agent_Train`'s eight arenas, each with a PlayerAgent where the PlayerBot was and the
  boss on Inference Only with Boss_v2. The boss starts every fight (Auto Restart off), so only `Player` trains.
- `config/imitation.yaml` (repo root): a first training config. Recordings go in `demos/` at the repo root.

## What the agent sees (39 floats, all about -1..1)

Only what a person can see on screen. The boss is seen 0.15 s late (Reaction Delay), like a person's reaction time.

| Group | Values |
|---|---|
| Itself (9) | health, stamina, attack phase one-hot (4), progress through it, damage of its current swing, rolling |
| The boss (17) | health, state one-hot (4), current attack one-hot (6), attack phase one-hot (4), progress through it, stun left |
| Where things are (9) | distance to the boss, the boss's facing (2), the boss's velocity (2), its own velocity (2), its position from the arena center (2) |
| The boss's shot (3) | one in flight, its distance, whether it is heading at the player |
| Round (1) | time left |

Directions are in the boss's frame: forward is toward the boss, right is to the right of that line. So "circle
left" means the same thing wherever the fight is.

## Actions

- Two continuous: move toward the boss, and move right of the line to it. Longer than 1 is scaled back to 1, like a
  stick.
- Attack branch: none, light, heavy. Held until the next decision, like holding the button.
- Roll branch: no, yes. A tap.

It decides every 2 physics steps (0.04 s) and repeats the last action in between.

**Changing what the agent sees or does makes every recording useless**, because recordings store exactly these
numbers. Settle any change before we record.

## Recording yourself

1. Open `Scenes/Imitation_Record.unity`.
2. Select Player. On Demonstration Recorder, tick Record and set Demonstration Name to your name. Set Demonstration
   Directory to `../demos/` plus your name, for example `../demos/max`. The path is relative to the Unity project
   folder, so this lands in `demos/max/` at the repo root.
3. Press Play and fight Boss_v2 normally (same controls as Agent_Play). Every press of Play adds one `.demo` file.
   Stopping Play saves it.
4. Aim for 10 to 15 minutes per person over a few sessions. Play how you normally would, including losing.
5. Untick Record before you play just for testing, or delete the extra files.

Commit your `demos/<name>/` folder.

## Training (once there are recordings)

From the repo root, then press Play in `Scenes/Imitation_Train.unity`:

```
uv run mlagents-learn config/imitation.yaml --run-id=player_v0
```

`demo_path` in the config (both places) picks the recordings: one person's folder trains a bot of that person,
`demos` trains on everyone. In TensorBoard, `Player/WinRate` and `Player/FightLength` show how it fights the boss, and
the GAIL reward shows how much it still looks like the recordings. The model lands in `results/<run-id>/Player.onnx`.
To watch it, put that model on a PlayerAgent with Behavior Type Inference Only.
