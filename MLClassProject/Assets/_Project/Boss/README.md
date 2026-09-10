# Boss

The boss body: five attacks with visible windups, locomotion around the player, and one small API for whoever drives it.
See `Scenes/Boss_Sandbox.unity` to play the boss by hand.

## Driving it

```csharp
body.CanPerform(BossMove.HeavySlam);   // not busy, not stunned, not dead, off cooldown
body.TryPerform(BossMove.Advance);     // locomotion sticks until another move replaces it; None stops
body.TryPerform(BossMove.QuickAttack); // false if it cannot start right now

body.State;                            // Idle, Attacking, Stunned, Dead
body.Phase; body.TimeInPhase;          // Combat's Windup / Active / Recovery while attacking
body.CooldownRemaining(move);          // seconds, 0 when ready
body.Target;                           // the player; found by the Player tag if not set

body.AttackPhaseChanged += (move, phase) => { };   // Windup, Active, Recovery, then Idle
body.StateChanged += state => { };
body.ResetForEpisode();                // stop, full health, cooldowns cleared; the arena moves the body
```

`BossMove` in Core is the whole action space: `None`, `Advance`, `Retreat`, `StrafeLeft`, `StrafeRight`, then the attacks.
The RL agent (T6) calls `TryPerform` with its chosen move each decision and masks with `CanPerform`.

## The moves

Every number is in a `BossMoveData` asset in `Data/` (Create → BossFight → Boss Move). It is an `AttackData` (Combat
runs the timing) plus cooldown, hit shape, stun, and projectile settings. First-pass values:

| Move | Windup | Active | Recovery | Cooldown | Hit shape | Damage | Punishes |
|---|---|---|---|---|---|---|---|
| QuickAttack | 0.3 | 0.1 | 0.4 | 0.5 | sphere r0.8, 1.2 m ahead | 8 | staying in and swinging |
| HeavySlam | 1.1 | 0.1 | 0.9 | 3 | sphere r2, 2 m ahead | 25 | rolling too early |
| SuperAttack | 2.0 | 0.2 | 0.6, then 3 s stun at 2x damage taken | 12 | sphere r4, 3.5 m ahead | 45 | hanging back |
| RangedShot | 0.6 | projectile | 0.5 | 4 | r0.5, 12 m/s, 20 m | 12 | standing far, not strafing |
| AoeBurst | 0.7 | 0.1 | 1.0 | 8 | sphere r4 on the boss | 15 | dodging too often |

Cooldowns count from the end of the move. The boss turns toward the player at full speed while idle, slowly during a
windup, and not at all from Active until the move ends.

## What is on the prefab

`Prefabs/Boss.prefab`: `CharacterController`, `Health` (300), `Hurtbox`, `AttackRunner`, `BossBody`, `BossTelegraph`.
Children: `Visual` (capsule), `Visor` (shows facing), `MeleeHitbox` (one `Hitbox` on the BossHitbox layer, reshaped per
move), `Muzzle`, `TelegraphDisc`, `TelegraphLine`.

`BossTelegraph` tints the body coral on windup, white on the hit, grey-blue in recovery, plum while stunned, and shows
the hit area on the ground during the windup. `Prefabs/BossProjectile.prefab` is the ranged shot: a `Hitbox` that flies
straight and disappears on the first hit or at range.

## Sandbox

`Scenes/Boss_Sandbox.unity`: the boss with T7's `PlayerInput` + `UserInput` and a `BossIntentDriver`, a dummy on the Player
layer, and a `FightEventLogger` printing hits. Keys: stick or WASD toward the dummy is Advance, away is Retreat, sideways
strafes. J or left click is Quick, K or right click is Slam, 1 is Super, 2 is Ranged, 3 is AoE. Hold a key to repeat.

## Tests

Window → General → Test Runner. Edit Mode: `BossMoveSetTests` (state and cooldown rules). Play Mode: `BossBodyTests`
builds a boss in code and checks every attack lands exactly once at time scale 1 and 20, the stun after the super, and
that Advance stops short of the target.

Not yet: animations, stagger when hit, head tracking. `AttackRunner.Interrupt()` is the hook for stagger.
