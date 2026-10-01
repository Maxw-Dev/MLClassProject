using UnityEngine;

namespace BossFight.ImitationBot
{
    /// <summary>
    /// The last half second of something, one value per physics step. <see cref="Seen"/> hands back the value from a
    /// moment ago, so an agent can react a human reaction time late. Plain C#, so it can be tested without a scene.
    /// </summary>
    public class DelayLine<T> where T : struct
    {
        /// <summary>The longest delay, in seconds, that it keeps enough values for.</summary>
        public const float MaxDelay = 0.5f;

        readonly float[] times;
        readonly T[] values;
        int newest = -1, count;

        /// <param name="stepSeconds">Seconds between values, normally Time.fixedDeltaTime.</param>
        public DelayLine(float stepSeconds)
        {
            int size = Mathf.CeilToInt(MaxDelay / Mathf.Max(stepSeconds, 0.001f)) + 2;
            times = new float[size];
            values = new T[size];
        }

        public int Count => count;

        /// <summary>Forgets everything, for a new fight.</summary>
        public void Clear()
        {
            newest = -1;
            count = 0;
        }

        /// <summary>True when there is already a value for this time. Record once per physics step.</summary>
        public bool Has(float time) => count > 0 && time <= times[newest];

        public void Record(float time, T value)
        {
            if (Has(time)) return;
            newest = (newest + 1) % values.Length;
            times[newest] = time;
            values[newest] = value;
            count = Mathf.Min(count + 1, values.Length);
        }

        /// <summary>
        /// The newest value recorded at or before <paramref name="time"/>. Early in a fight, when there is none that
        /// old, the first value of the fight.
        /// </summary>
        public T Seen(float time)
        {
            for (int back = 0; back < count; back++)
            {
                int i = (newest - back + values.Length) % values.Length;
                if (times[i] <= time + 0.0001f) return values[i];
            }
            return count > 0 ? values[(newest - count + 1 + values.Length) % values.Length] : default;
        }
    }
}
