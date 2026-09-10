# Combat

Health, stamina, and hits, shared by the player and the boss. Pure rules: no animation, no input.
See `Scenes/Combat_Sandbox.unity` for two capsules using everything below.

## Putting it on a body

On the body root:
- `Health` — hit points. Raises `FightEvents.OnHit` per landed hit and `FightEvents.OnDeath` once.
- `Stamina` — spent by attacks (and rolls, if the body wants), regenerates after a short delay.
- `Hurtbox` — on the body's physical collider, or any child collider (colliders below it count too).
  Routes hits to the nearest `IDamageable` up the hierarchy, normally the `Health` on the root.
  The collider must be on the body's layer (`Player` or `Boss`).
- `AttackRunner` — plays an `AttackData` timeline. Point it at the hitbox below and the `Stamina`.

On a child object:
- `Hitbox` — a sphere of damage (offset + radius, no collider needed) on the `PlayerHitbox` or `BossHitbox` layer.
  One per weapon or fist; add more children if a move needs coverage. It runs its own overlap query each physics
  step while armed. It reads its layer once in `Awake`, so set the layer on the prefab, not at spawn time.

## Using it

```csharp
runner.TryStart(lightAttack);            // false if busy, disabled, or out of stamina
runner.CanStart(heavyAttack);            // ask first if you need to
runner.Phase;                            // Idle, Windup, Active, Recovery
runner.CurrentAttack; runner.TimeInPhase;
runner.Interrupt();                      // stagger, death, episode reset
runner.TryStartUnarmed(rangedShot);      // timeline only, no hitbox: the move spawns its own damage (see Boss's projectile)

runner.PhaseChanged += (attack, phase) => { };   // Windup, Active, Recovery, then Idle when it ends
runner.Finished += (attack, interrupted) => { }; // interrupted is true when cut short

health.GrantInvulnerability(0.4f);       // call at the start of a roll
health.IncomingDamageMultiplier = 2f;    // while exposed (the boss's stun); ResetToFull puts it back to 1
health.Damaged += info => { };           // a hit landed
health.Dodged += info => { };            // a hit was ignored by i-frames
health.Died += () => { };
health.IsDead; health.Normalized;        // for HUD and observations

stamina.TrySpend(cost); stamina.Normalized;

hitbox.Hit += (victim, info) => { };     // this hitbox reached someone (they may still have i-framed it)
```

**Episode reset** (arena / fight manager): `runner.Interrupt(); health.ResetToFull(); stamina.ResetToFull();`
Resets are silent; nothing fires. Re-read state afterwards if you display it.

**Timing** is ticked in `FixedUpdate`, so phase lengths are exact at any time scale, including headless training at 20x.
Keep `ActiveSeconds` at or above the fixed timestep (0.02 s) or the hitbox never gets a step while armed.

**Attack data** lives in `AttackData` assets (Create → BossFight → Attack Data). `Data/` has a Light and a Heavy to start from.
`AttackData.Range` is informational (for AI and observations); the hitbox sphere is what actually hits.

**Events:** `FightEvents.OnHit(attacker, victim, info)` and `FightEvents.OnDeath(victim)` in Core. Subscribe in `OnEnable`,
unsubscribe in `OnDisable`. They are static, so they clear themselves when play starts; a missed unsubscribe still means
a dead listener until then.

Not in v1: knockback, poise/stagger, blocking. `AttackRunner.Interrupt()` is the hook for stagger when someone needs it.

## Tests

Window → General → Test Runner. Edit Mode covers the health, stamina, and attack-timeline math. Play Mode runs a real
two-capsule fight and checks the outcome is identical at time scale 1 and 20.
