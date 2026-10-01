using BossFight.Combat;
using UnityEngine;

namespace BossFight.RL
{
    /// <summary>
    /// What the boss has seen of its opponent, one <see cref="Sighting"/> per physics step. <see cref="Seen"/> hands back
    /// the sighting from a moment ago, so the boss reacts a human reaction time late instead of on the frame a button
    /// is pressed. It also keeps the opponent's habits from T10's input list: how long since its last roll, light
    /// attack and heavy attack, and the usual gap between uses of each. Plain C#, so it can be tested without a scene.
    /// </summary>
    public class OpponentMemory
    {
        /// <summary>The longest delay, in seconds, that the memory keeps enough sightings for.</summary>
        public const float MaxDelay = 0.5f;
        /// <summary>Seconds a habit starts at when a fight begins, as if it was last used long ago.</summary>
        public const float NeverUsed = 20f;

        public enum Habit { Roll, LightAttack, HeavyAttack }
        public const int HabitCount = 3;

        /// <summary>The opponent at one physics step. The caller fills in what it sees, <see cref="Record"/> adds the rest.</summary>
        public struct Sighting
        {
            public float Time;
            public Vector3 Position, Forward;
            public float Health, Stamina;           // 0..1
            public AttackPhase Phase;
            public float PhaseProgress;             // 0..1 through the current phase
            public float AttackDamage;              // damage of the swing in progress, 0 when idle
            public bool Invulnerable;               // rolling
            public Vector3 Velocity;                // added by Record
            internal float sinceRoll, sinceLight, sinceHeavy, gapRoll, gapLight, gapHeavy;   // added by Record

            /// <summary>Seconds since the opponent last started this.</summary>
            public float SinceLast(Habit habit) =>
                habit == Habit.Roll ? sinceRoll : habit == Habit.LightAttack ? sinceLight : sinceHeavy;

            /// <summary>
            /// The usual gap between uses, in seconds. It grows like <see cref="SinceLast"/> and is divided by 3 on every
            /// use, so a habit used every T seconds settles between T/2 and 1.5 T. Lower: the opponent does it more often.
            /// </summary>
            public float UsualGap(Habit habit) =>
                habit == Habit.Roll ? gapRoll : habit == Habit.LightAttack ? gapLight : gapHeavy;
        }

        readonly Sighting[] sightings;
        int newest = -1, count;
        readonly float[] sinceLast = new float[HabitCount];
        readonly float[] usualGap = new float[HabitCount];

        /// <param name="stepSeconds">Seconds between sightings, normally Time.fixedDeltaTime.</param>
        public OpponentMemory(float stepSeconds)
        {
            sightings = new Sighting[Mathf.CeilToInt(MaxDelay / Mathf.Max(stepSeconds, 0.001f)) + 2];
            Clear();
        }

        /// <summary>Forgets every sighting and habit, for a new fight.</summary>
        public void Clear()
        {
            newest = -1;
            count = 0;
            for (int i = 0; i < HabitCount; i++)
            {
                sinceLast[i] = NeverUsed;
                usualGap[i] = NeverUsed;
            }
        }

        /// <summary>The opponent just started a roll or an attack.</summary>
        public void Used(Habit habit)
        {
            sinceLast[(int)habit] = 0f;
            usualGap[(int)habit] /= 3f;
        }

        /// <summary>True when there is already a sighting for this time. Record once per physics step.</summary>
        public bool Has(float time) => count > 0 && time <= sightings[newest].Time;

        /// <summary>Keeps this step's sighting, with the velocity since the last one and the habits as they are now.</summary>
        public void Record(Sighting sighting)
        {
            if (Has(sighting.Time)) return;
            float step = 0f;
            sighting.Velocity = Vector3.zero;   // the first sighting of a fight: no velocity from the teleport to the spawn
            if (count > 0)
            {
                var last = sightings[newest];
                step = sighting.Time - last.Time;
                sighting.Velocity = (sighting.Position - last.Position) / step;
            }
            for (int i = 0; i < HabitCount; i++)
            {
                sinceLast[i] += step;
                usualGap[i] += step;
            }
            sighting.sinceRoll = sinceLast[(int)Habit.Roll];
            sighting.sinceLight = sinceLast[(int)Habit.LightAttack];
            sighting.sinceHeavy = sinceLast[(int)Habit.HeavyAttack];
            sighting.gapRoll = usualGap[(int)Habit.Roll];
            sighting.gapLight = usualGap[(int)Habit.LightAttack];
            sighting.gapHeavy = usualGap[(int)Habit.HeavyAttack];

            newest = (newest + 1) % sightings.Length;
            sightings[newest] = sighting;
            count = Mathf.Min(count + 1, sightings.Length);
        }

        /// <summary>
        /// The newest sighting at or before <paramref name="time"/>. Early in a fight, when there is none that old, the
        /// first sighting of the fight.
        /// </summary>
        public Sighting Seen(float time)
        {
            for (int back = 0; back < count; back++)
            {
                var sighting = sightings[(newest - back + sightings.Length) % sightings.Length];
                if (sighting.Time <= time + 0.0001f) return sighting;
            }
            return count > 0 ? sightings[(newest - count + 1 + sightings.Length) % sightings.Length] : default;
        }
    }
}
