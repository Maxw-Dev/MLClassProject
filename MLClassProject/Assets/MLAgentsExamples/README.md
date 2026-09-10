# ML-Agents example environments (test branch only)

These are the stock example environments from the ml-agents repo, copied from the exact
commit our `com.unity.ml-agents` 4.1.0 package was built from
(`ee0a08ccae597094003844d0121317f9790a1676`). They exist so we can see working agents,
training configs, and reward design before building our own. Lives in `Assets/MLAgentsExamples/` because `Assets/ML-Agents/` is git-ignored (ML-Agents writes timer json there). **Do not merge this folder
into Development.**

## Try a pre-trained agent (no Python needed)

1. Open a scene, e.g. `3DBall/Scenes/3DBall.unity` or `PushBlock/Scenes/PushBlock.unity`.
2. Press Play. Each agent prefab already has a trained `.onnx` model assigned, so it runs in inference mode.
3. Good ones to look at for our boss fight: `Soccer` (self-play, adversarial), `DungeonEscape` (agents vs a dragon, POCA),
   `Pyramids` (curiosity reward), `WallJump` (curriculum), `Hallway` (memory / LSTM).

## Train one yourself

From the **repo root**:

```
uv run mlagents-learn config/mlagents-examples/ppo/3DBall.yaml --run-id=3dball_test
```

When it says it is listening, press Play in the matching scene. Output lands in `results/`.
Configs for every example are in `config/mlagents-examples/{ppo,sac,poca,imitation}/`.

## What was changed to make them run here

- **Tags:** the example tags were added to `ProjectSettings/TagManager.asset`.
- **Layer 8:** the examples name layer 8 `invisible`; our project already uses 8 for `PlayerHitbox`.
  GridWorld's objects were remapped to layer 10, now named `invisible`.
- **Active Input Handling:** set to *Both* (was *Input System Package*). The example
  heuristics use the legacy `Input` class, which throws when the new system is the only one enabled.
  Unity will ask to restart the first time you open the project on this branch.
- **Materials:** the examples were authored for the built-in render pipeline and we use URP, so
  they import pink. One-time fix, in the editor:
  `Window > Rendering > Render Pipeline Converter`, pick *Built-in to URP*, tick *Material Upgrade*,
  then *Initialize and Convert*.
- `SharedAssets/Materials/GridPatternShader.shader` and `ShaderOutline.shader` were built-in surface/CG shaders, which the converter cannot upgrade, so the six materials on them (GridMatFloor, GridMat, GridMatBall, GridMatMoveable, GridMatHallway, Outline) stayed pink. Both were ported to URP HLSL in place with the same names and properties.
- The PushBlockWithInput `.inputactions` meta pointed its generated C# wrapper at the old `Assets/ML-Agents/Examples/` path, which produced a duplicate class; it now points at this folder.
- The imitation-learning configs point their `demo_path` at this folder instead of the ml-agents repo layout.
