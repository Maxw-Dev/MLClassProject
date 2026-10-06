# ImitationBot

A player bot that learns from watching you play (T18). You record yourself fighting the trained boss. ML-Agents
first copies what you did, then keeps practising against the boss until it wins. Everyone can make their own bot,
and we can compare them, train the boss against them, and use them in the writeup.

## Make your own bot, step by step

Everything below is done once per person. Replace `yourname` with your name in lowercase, for example `james`.

### 1. Record yourself (about 15 minutes)

1. Open `ImitationBot/Scenes/Imitation_Record.unity`.
2. Click **Player** in the Hierarchy. In the Inspector, find **Demonstration Recorder** and set:
   - **Record**: ticked
   - **Demonstration Name**: `yourname`
   - **Demonstration Directory**: `../demos/yourname`
3. Press Play and fight the boss the way you normally play. Losing is fine.
4. Press Play again to stop. That saves one recording file into `demos/yourname/` at the top of the repo.
5. Do this a few times until you have about 10 to 15 minutes of fighting. Only fighting counts: the pause between
   rounds is not recorded.
6. Untick **Record** when you are done, so later test plays do not add files.

### 2. Make your own training settings

Copy `config/imitation.yaml` (in the repo root) to `config/imitation_yourname.yaml`. In the copy, change both lines
that say `demo_path:` so they point to your folder:

```yaml
        demo_path: demos/yourname
```

### 3. Train your bot (about an hour, or stop early)

1. In a terminal at the repo root, run:
   ```
   uv run mlagents-learn config/imitation_yourname.yaml --run-id=yourname_v1
   ```
2. When it says it is waiting for Unity, open `ImitationBot/Scenes/Imitation_Train.unity` and press Play. Eight
   arenas start fighting at high speed. Leave Unity alone while it trains.
3. To see how it is doing, run this in a second terminal and open the link it prints:
   ```
   uv run tensorboard --logdir results
   ```
   Look at **Player/WinRate**. Max's bot passed 90% after about 8 minutes of training.
4. It stops by itself after 2 million steps (about 50 minutes). You can stop earlier with Ctrl+C in the terminal.
   It saves either way.

Your bot is saved as `results/yourname_v1/Player.onnx`.

### 4. Watch your bot

1. Copy `results/yourname_v1/Player.onnx` into `ImitationBot/Models/` and rename it `Player_yourname.onnx`.
2. Open `ImitationBot/Scenes/Imitation_Watch.unity`.
3. Click **PlayerBot (imitation)** in the Hierarchy. In **Behavior Parameters**, drag your file into **Model**.
4. Press Play. The bot fights the boss, with a short pause between rounds.

Use `Imitation_Watch` for watching. Do not change the players in `Imitation_Train` or `Imitation_Record`, or the
next training run or recording will not work.

### 5. Share it

Commit your `demos/yourname/` folder, `config/imitation_yourname.yaml` and `ImitationBot/Models/Player_yourname.onnx`.

### If something goes wrong

- **"Previous data from this run ID"**: that run name is used already. Pick a new one (`yourname_v2`) or add
  `--force` to start it over.
- **"behavior name Player has not been specified"** or nothing happens: you pressed Play in the wrong scene. Training
  needs `Imitation_Train`.
- **An error about the demonstration not matching**: the recording was made before the 1 October change to how
  walking works. Delete it and record again.
- **The bot stands still**: in `Imitation_Watch`, check that a model is set and Behavior Type is Inference Only.

## Results so far

| Run | What changed | Bot's win rate vs Boss_v2 | Fight length |
|---|---|---|---|
| Max by hand | his 24 recorded fights | 96% | short |
| `max_v0` | first try, walking as two numbers | about 40% | 45 s |
| `max_v1` | walking as 9 choices, 0.1 s decisions | about 40% | 41 s |
| `max_v3` | paid mainly for winning (GAIL 0.01, game reward 1.0) | 99% from 500k steps on | 17 s |

## How it works

### What is here

- `Scripts/PlayerAgent.cs`: the player's side as an ML-Agents Agent. Behavior name `Player`.
  - With Human Input set, a person drives the player as usual and the agent records what they do.
  - With it empty, the agent is the player's input and drives it.
  - It never starts fights. It waits for the arena (Auto Restart on) or the BossAgent (training), and makes no
    decisions between fights, so recordings only hold fighting.
- `Scripts/PlayerActions.cs`: how actions become an `Intent` and back. Tested in `Tests/`.
- `Scripts/DelayLine.cs`: keeps the last half second of boss sightings, for the reaction delay. Tested in `Tests/`.
- `Prefabs/PlayerAgent.prefab`: Player variant for training and playing the bot. The keyboard input is removed and
  Behavior Parameters (`Player`, Behavior Type Default) and `PlayerAgent` are added.
- `Scenes/Imitation_Record.unity`: `Agent_Play` (you vs Boss_v2) with the agent and a Demonstration Recorder on the
  player.
- `Scenes/Imitation_Train.unity`: `Agent_Train`'s eight arenas, each with a PlayerAgent where the PlayerBot was and the
  boss on Inference Only with Boss_v2. The boss starts every fight (Auto Restart off), so only `Player` trains.
- `Scenes/Imitation_Watch.unity`: one arena, a PlayerAgent on Inference Only with Deterministic Inference (always its
  most likely choice) against Boss_v2, Auto Restart on.
- `Models/`: trained player bots. `Player_v3.onnx` is `max_v3` (99% against Boss_v2). `Player_v2.onnx` is `max_v1`
  (about 40%), kept for comparison.
- `config/imitation.yaml` (repo root) and `demos/` (repo root): the training settings and the recordings.

### What the agent sees (39 floats, all about -1..1)

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

### Actions

Three choices each decision, all discrete:

- Walk (9): stand still, or one of 8 directions around the boss (toward, toward-right, right, away-right, away,
  away-left, left, toward-left), always at full speed like a keyboard. A person's movement is recorded as the nearest
  of the 8.
- Attack (3): none, light, heavy. Held until the next decision, like holding the button.
- Roll (2): no, yes. A tap.

It decides every 5 physics steps (0.1 s, like the boss) and repeats the last action in between. Presses between two
decisions are still recorded.

Why walking is a choice and not two numbers: the first bot (`max_v0`) had movement as two numbers. Learning from
recordings, two numbers become the average of what people did, and the average of "walk in" and "back off" is
"barely move". It moved at about a quarter of the recorded speed, and the random noise ML-Agents adds to numbers
during play made it stutter. A choice picks one direction and moves at full speed.

**Changing what the agent sees or does makes every recording useless**, because recordings store exactly these
numbers. Settle any change before we record.

### Copying versus winning

Training mixes three signals. Behavioral cloning copies the recordings for the first 150k steps and then fades out.
After that the game's own reward (`extrinsic`: winning and damage) leads, and GAIL (`gail`) adds a small extra reward
for playing like the recordings. Keep GAIL small (0.01). At GAIL 0.5 and the game's reward 0.1 (`max_v0`, `max_v1`),
copying outweighed winning about 1000 to 1 and the bot won about 40%. ML-Agents' docs give the same advice: with
human recordings, keep GAIL below about 0.1 so the agent goes for the reward instead of copying people's mistakes.

A bot that always beats one boss is not the end goal. Any trained model has weak spots, and a boss trained against
one fixed bot learns to exploit them. Next step: train the boss against these bots, then retrain the bots against
the new boss, and repeat.
