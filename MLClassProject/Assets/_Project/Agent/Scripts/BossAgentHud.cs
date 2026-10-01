using System;
using System.Text;
using BossFight.Boss;
using BossFight.Combat;
using BossFight.Core;
using UnityEngine;

namespace BossFight.RL
{
    /// <summary>
    /// Debug overlay for the agent scenes: the round clock, both healths, the agent's last move, which moves it may pick
    /// next, and its reward so far. Read only. Put it anywhere in the scene and point it at a BossAgent.
    /// </summary>
    public class BossAgentHud : MonoBehaviour
    {
        static readonly BossMove[] Moves = (BossMove[])Enum.GetValues(typeof(BossMove));

        [SerializeField] BossAgent agent;

        readonly StringBuilder text = new StringBuilder();
        Health bossHealth;
        Health opponentHealth;
        GUIStyle style;

        void Start()
        {
            if (agent == null) return;
            bossHealth = agent.GetComponent<Health>();
            if (agent.Opponent != null) opponentHealth = agent.Opponent.GetComponent<Health>();
        }

        void OnGUI()
        {
            if (agent == null || agent.Body == null || bossHealth == null) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, padding = new RectOffset(10, 10, 8, 8) };

            text.Clear();
            var arena = agent.Arena;
            if (arena != null)
            {
                int seconds = Mathf.CeilToInt(arena.TimeRemaining);
                text.Append("Round ").Append(arena.CurrentRound).Append("   ")
                    .Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00"))
                    .AppendLine(arena.IsFightActive ? " left" : ", over");
            }
            text.Append("Boss ").Append(Mathf.CeilToInt(bossHealth.Current)).Append(" / ").Append(bossHealth.Max);
            if (opponentHealth != null)
                text.Append("      ").Append(agent.Opponent.name).Append(' ')
                    .Append(Mathf.CeilToInt(opponentHealth.Current)).Append(" / ").Append(opponentHealth.Max);
            text.AppendLine();
            text.Append("Boss is ").Append(agent.Body.State).Append(", last move ").Append(agent.LastMove).AppendLine();
            text.Append("Allowed:");
            foreach (var move in Moves)
                if (agent.Body.CanPerform(move)) text.Append(' ').Append(move);
            text.AppendLine();
            text.Append("Reward this episode ").Append(agent.GetCumulativeReward().ToString("+0.00;-0.00"))
                .Append("   episodes ").Append(agent.CompletedEpisodes).AppendLine();
            text.Append("Last fight: ").Append(agent.LastResult);

            var content = new GUIContent(text.ToString());
            var size = style.CalcSize(content);
            GUI.Box(new Rect(10f, 10f, size.x, size.y), content, style);
        }
    }
}
