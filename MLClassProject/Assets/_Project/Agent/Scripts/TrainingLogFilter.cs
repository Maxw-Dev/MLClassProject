using UnityEngine;

namespace BossFight.RL
{
    /// <summary>
    /// For the training scene: hides info-level Debug.Log messages while the scene runs. Warnings and errors still show.
    /// With many fighters at 20x speed, per-hit log lines are the slowest thing in the scene.
    /// </summary>
    public class TrainingLogFilter : MonoBehaviour
    {
        LogType previous;

        void Awake()
        {
            previous = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Warning;
        }

        void OnDestroy() => Debug.unityLogger.filterLogType = previous;
    }
}
