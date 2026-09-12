# MLClassProject
Class Project for CS Machine Learning A Term 2026 

## Project layout and ownership

Game code lives in `MLClassProject/Assets/_Project/`. One folder per ticket, one assembly per folder, one owner per folder. Work inside your folder; if you need something changed elsewhere, ask its owner.

| Folder | Assembly | May reference | Ticket |
|---|---|---|---|
| `_Project/Core` | `BossFight.Core` | nothing | T1 · Max |
| `_Project/Combat` | `BossFight.Combat` | Core | T2 · Max |
| `_Project/Player` | `BossFight.Player` | Core, Combat | T3 |
| `_Project/Boss` | `BossFight.Boss` | Core, Combat | T4 |
| `_Project/Arena` | `BossFight.Arena` | Core, Combat | T5 |
| `_Project/Agent` | `BossFight.Agent` | Core, Combat, Boss, ML-Agents | T6 · Max |
| `_Project/Input` | `BossFight.Input` | Core, Input System, Cinemachine | T7 · Glove |
| `tools/`, `config/` (repo root) | Python, no assembly | — | T8 · Training tooling |
| `_Project/Telemetry` | `BossFight.Telemetry` | Core | T9 · Episode logging |

- **Combat** is documented in `MLClassProject/Assets/_Project/Combat/README.md`: how to put health, stamina, hurtboxes, hitboxes, and attacks on a body.
- **Core** holds the few things everyone shares: `Intent` + `IIntentSource` (controller → body contract; human input, bot, and ML agent all produce an `Intent`, either body consumes it), `IDamageable`, `DamageInfo`, `AttackData`, `BossMove`, `FightEvents`. Change it by PR and tag the people it affects.
- **Scenes:** `_Project/Arena/Scenes/Arena.unity` is the only shared scene (T5 owns it). Everyone else tests in their own `<Area>/Scenes/<Area>_Sandbox.unity`.
- **Prefabs** belong to their folder's owner. To add something to someone else's prefab, make a prefab variant in your folder or ask.
- Need a package in your assembly (Input System, Cinemachine, Animation Rigging)? Add it to your own `.asmdef` references.
- Tags: `Player`, `Boss`. Layers: `Player`, `Boss`, `PlayerHitbox`, `BossHitbox`. Hitboxes only collide with the opposing body.
- Input: `Assets/InputSystem_Actions.inputactions`, Player map: Move, Look, LightAttack, HeavyAttack, Roll, LockOn, Sprint.

## Branching and pull requests

- `Development` is the integration branch. Branch from it and PR into it. `main` only gets updated from `Development` at milestones.
- One branch per ticket, named `feat/T<n>-<short-name>`, for example `feat/T3-player`.
- Rebase on `Development` often. Open a PR into `Development` when the ticket's "done when" is met. One reviewer, then merge and delete the branch.
- A ticket is unblocked when its blocker is **merged to Development**, not when it is done on a branch.
- Never commit `Library/`, `Logs/`, builds, or `results/`.

## Unity merge tool (one-time, per machine)

If two people do touch the same scene or prefab, Unity's Smart Merge resolves most of it. Register it once:

macOS (adjust the editor version to match `ProjectSettings/ProjectVersion.txt`):
```
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/Helpers/UnityYAMLMerge' merge -p %O %A %B %A"
```

Windows (PowerShell):
```
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Tools\UnityYAMLMerge.exe' merge -p %O %A %B %A"
```

## Checking your ML-Agents setup

After `uv sync`, prove Unity can talk to the trainer:

1. Open `MLClassProject/Assets/_Project/Agent/Scenes/Agent_Smoke.unity`.
2. From the repo root run:
   ```
   uv run mlagents-learn config/smoke.yaml --run-id=smoke_test --force
   ```
3. When the terminal says it is listening, press Play in Unity. You should see the trainer report it connected and print step summaries. Stop after a few seconds.

If that works, the full pipeline works on your machine.

## Python setup (ML-Agents training)

Everyone needs the **same** Python and package versions, because the Python trainer has to match the Unity package (`com.unity.ml-agents` 4.1.0). This repo pins both, so nobody installs Python by hand:

- `.python-version` pins **Python 3.10.12** (the only range ML-Agents supports is 3.10.1 - 3.10.12).
- `pyproject.toml` + `uv.lock` pin the ML-Agents Python packages and every transitive dependency.

We use [uv](https://docs.astral.sh/uv/) to manage this. It downloads the pinned Python for you.

### One-time setup per machine

1. Install uv:
   - macOS: `brew install uv` (or `curl -LsSf https://astral.sh/uv/install.sh | sh`)
   - Windows (PowerShell): `powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"`
2. From the **repo root** (the folder with this README), run:
   ```
   uv sync
   ```
   This downloads Python 3.10.12, creates `.venv/` (git-ignored), and installs exactly what is in `uv.lock`.
3. Check it worked:
   ```
   uv run mlagents-learn --help
   ```

### Training

Run commands through `uv run` so they always use the project's Python. From the repo root:

```
uv run mlagents-learn <path/to/config.yaml> --run-id=<run_name>
```

Then press Play in the Unity Editor. Training output lands in `results/` (git-ignored).

If you prefer an activated shell instead of `uv run`: `source .venv/bin/activate` (macOS/Linux) or `.venv\Scripts\activate` (Windows).

### TensorBoard Instructions

TensorBoard allows you to monitor training metrics (such as cumulative reward, episode length, and policy entropy) in real time.

Run this from the repo root, pointing to your results/ directory:
```
uv run tensorboard --logdir results
```
Or, to inspect a specific training run, point directly to its run ID folder:
```
uv run tensorboard --logdir results/<run_id>
```
Once running, open your browser and navigate to: `http://localhost:6006`


### Rules

- Do not `pip install` into the venv by hand. To add or change a dependency, edit `pyproject.toml`, run `uv lock`, and commit the updated `uv.lock`.
- After pulling changes that touch `pyproject.toml` or `uv.lock`, run `uv sync` again.
- Apple Silicon Macs: every `uv sync` / `uv run` prints `~ grpcio==1.53.2` (a reinstall). This is a harmless quirk of grpcio's macOS wheel being mislabeled as x86_64 internally; ignore it.
- Optional, Windows + NVIDIA GPU: the default install is CPU-only PyTorch, which is fine for this project. See the PyTorch install page if you want CUDA.
