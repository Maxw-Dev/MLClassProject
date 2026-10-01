using System;
using System.Collections.Generic;
using BossFight.Core;

namespace BossFight.Boss
{
    public enum BossState { Idle, Attacking, Stunned, Dead }

    /// <summary>
    /// The rules for what the boss may do right now: one state, one cooldown per attack, and a shared gap after any attack.
    /// No Unity lifecycle, so it is unit tested directly. <see cref="BossBody"/> feeds it time and the start and end of each
    /// attack. Cooldowns count from the moment a move ends (stun included), so "cooldown 3" means a three second gap.
    /// The shared gap also starts when any attack ends and blocks every attack (not movement) until it runs out, so the
    /// boss cannot chain different attacks back to back.
    /// </summary>
    public sealed class BossMoveSet
    {
        readonly Dictionary<BossMove, float> cooldownSeconds = new Dictionary<BossMove, float>();
        readonly Dictionary<BossMove, float> readyAt = new Dictionary<BossMove, float>();
        readonly float attackGapSeconds;
        float anyAttackReadyAt;
        float now;

        public BossState State { get; private set; } = BossState.Idle;
        public BossMove CurrentAttack { get; private set; } = BossMove.None;
        public float StunRemaining { get; private set; }

        /// <param name="attackGapSeconds">After any attack ends, every attack waits this long on top of its own cooldown.</param>
        public BossMoveSet(IEnumerable<(BossMove move, float cooldownSeconds)> attacks, float attackGapSeconds = 0f)
        {
            foreach (var (move, cooldown) in attacks)
                if (IsAttack(move)) cooldownSeconds[move] = Math.Max(0f, cooldown);
            this.attackGapSeconds = Math.Max(0f, attackGapSeconds);
        }

        public static bool IsLocomotion(BossMove move) =>
            move == BossMove.Advance || move == BossMove.Retreat || move == BossMove.StrafeLeft || move == BossMove.StrafeRight;

        public static bool IsAttack(BossMove move) => move != BossMove.None && !IsLocomotion(move);

        /// <summary>True when the attack has data, and so can ever be performed.</summary>
        public bool Knows(BossMove move) => cooldownSeconds.ContainsKey(move);

        /// <summary>Seconds until this attack can start again: its own cooldown or the shared gap, whichever ends later. 0 for movement.</summary>
        public float CooldownRemaining(BossMove move)
        {
            if (!IsAttack(move)) return 0f;
            float own = readyAt.TryGetValue(move, out var readyTime) ? readyTime - now : 0f;
            return Math.Max(0f, Math.Max(own, anyAttackReadyAt - now));
        }

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

        /// <summary>The attack ended, finished or interrupted. Its cooldown and the shared gap start now.</summary>
        public void EndAttack()
        {
            if (State != BossState.Attacking) return;
            if (cooldownSeconds.TryGetValue(CurrentAttack, out var cooldown)) readyAt[CurrentAttack] = now + cooldown;
            anyAttackReadyAt = now + attackGapSeconds;
            CurrentAttack = BossMove.None;
            State = BossState.Idle;
        }

        /// <summary>Knocked out of whatever it was doing (a hit during an interruptible windup). Helpless for <paramref name="seconds"/>.</summary>
        public void Stun(float seconds)
        {
            if (State == BossState.Dead || seconds <= 0f) return;
            CurrentAttack = BossMove.None;
            State = BossState.Stunned;
            StunRemaining = seconds;
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

        /// <summary>Episode reset: Idle, no cooldowns, no shared gap, no stun.</summary>
        public void Reset()
        {
            State = BossState.Idle;
            CurrentAttack = BossMove.None;
            StunRemaining = 0f;
            readyAt.Clear();
            anyAttackReadyAt = 0f;
        }
    }
}
