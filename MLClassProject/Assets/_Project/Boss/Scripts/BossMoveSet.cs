using System;
using System.Collections.Generic;
using BossFight.Core;

namespace BossFight.Boss
{
    public enum BossState { Idle, Attacking, Stunned, Dead }

    /// <summary>
    /// The rules for what the boss may do right now: one state, one cooldown per attack. No Unity lifecycle, so it is
    /// unit tested directly. <see cref="BossBody"/> feeds it time and the start and end of each attack.
    /// Cooldowns count from the moment a move ends (stun included), so "cooldown 3" means a three second gap.
    /// </summary>
    public sealed class BossMoveSet
    {
        readonly Dictionary<BossMove, float> cooldownSeconds = new Dictionary<BossMove, float>();
        readonly Dictionary<BossMove, float> readyAt = new Dictionary<BossMove, float>();
        float now;

        public BossState State { get; private set; } = BossState.Idle;
        public BossMove CurrentAttack { get; private set; } = BossMove.None;
        public float StunRemaining { get; private set; }

        public BossMoveSet(IEnumerable<(BossMove move, float cooldownSeconds)> attacks)
        {
            foreach (var (move, cooldown) in attacks)
                if (IsAttack(move)) cooldownSeconds[move] = Math.Max(0f, cooldown);
        }

        public static bool IsLocomotion(BossMove move) =>
            move == BossMove.Advance || move == BossMove.Retreat || move == BossMove.StrafeLeft || move == BossMove.StrafeRight;

        public static bool IsAttack(BossMove move) => move != BossMove.None && !IsLocomotion(move);

        /// <summary>True when the attack has data, and so can ever be performed.</summary>
        public bool Knows(BossMove move) => cooldownSeconds.ContainsKey(move);

        public float CooldownRemaining(BossMove move) =>
            readyAt.TryGetValue(move, out var readyTime) ? Math.Max(0f, readyTime - now) : 0f;

        /// <summary>None is always fine unless dead. Everything else needs Idle; attacks also need data and no cooldown left.</summary>
        public bool CanPerform(BossMove move)
        {
            if (State == BossState.Dead) return false;
            if (move == BossMove.None) return true;
            if (State != BossState.Idle) return false;
            if (IsLocomotion(move)) return true;
            return Knows(move) && CooldownRemaining(move) <= 0f;
        }

        public void BeginAttack(BossMove move)
        {
            State = BossState.Attacking;
            CurrentAttack = move;
        }

        /// <summary>The attack never actually started. Back to Idle, no cooldown.</summary>
        public void CancelAttack()
        {
            if (State != BossState.Attacking) return;
            CurrentAttack = BossMove.None;
            State = BossState.Idle;
        }

        /// <summary>The attack ended. Its cooldown starts now; a stun follows when <paramref name="stunSeconds"/> is above 0.</summary>
        public void EndAttack(float stunSeconds)
        {
            if (State != BossState.Attacking) return;
            if (cooldownSeconds.TryGetValue(CurrentAttack, out var cooldown)) readyAt[CurrentAttack] = now + cooldown;
            CurrentAttack = BossMove.None;
            if (stunSeconds > 0f)
            {
                State = BossState.Stunned;
                StunRemaining = stunSeconds;
            }
            else
            {
                State = BossState.Idle;
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            now += deltaTime;
            if (State != BossState.Stunned) return;
            StunRemaining -= deltaTime;
            if (StunRemaining <= 0f)
            {
                StunRemaining = 0f;
                State = BossState.Idle;
            }
        }

        public void Kill()
        {
            State = BossState.Dead;
            CurrentAttack = BossMove.None;
            StunRemaining = 0f;
        }

        /// <summary>Episode reset: Idle, no cooldowns, no stun.</summary>
        public void Reset()
        {
            State = BossState.Idle;
            CurrentAttack = BossMove.None;
            StunRemaining = 0f;
            readyAt.Clear();
        }
    }
}
