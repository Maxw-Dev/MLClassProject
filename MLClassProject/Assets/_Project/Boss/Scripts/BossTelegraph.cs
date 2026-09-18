using BossFight.Combat;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// Makes the boss readable without animations: tints the body by phase and shows the hit area on the ground during
    /// the windup. Coral is reserved for telegraphs in the art direction, so coral means "about to hit".
    /// </summary>
    public class BossTelegraph : MonoBehaviour
    {
        [SerializeField] BossBody body;
        [SerializeField] Renderer bodyRenderer;
        [Tooltip("A flat disc child of the boss root, disabled by default. Scaled to a melee or AoE hit sphere.")]
        [SerializeField] Transform disc;
        [Tooltip("A flat bar child of the boss root, disabled by default. Stretched along the projectile's path.")]
        [SerializeField] Transform line;

        [Header("Tints")]
        [SerializeField] Color windup = new Color(1f, 0.42f, 0.42f);       // coral
        [SerializeField] Color active = Color.white;
        [SerializeField] Color recovery = new Color(0.55f, 0.6f, 0.7f);
        [SerializeField] Color stunned = new Color(0.23f, 0.18f, 0.35f);   // plum ink
        [SerializeField] Color dead = new Color(0.3f, 0.3f, 0.3f);

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        MaterialPropertyBlock block;
        Color normal = Color.white;

        void Awake()
        {
            if (body == null) body = GetComponentInParent<BossBody>();
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            var material = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
            if (material != null)
                normal = material.HasProperty(BaseColor) ? material.GetColor(BaseColor)
                       : material.HasProperty(LegacyColor) ? material.GetColor(LegacyColor) : Color.white;
            Hide();
        }

        void OnEnable()
        {
            if (body == null) return;
            body.AttackPhaseChanged += OnPhase;
            body.StateChanged += OnState;
        }

        void OnDisable()
        {
            if (body == null) return;
            body.AttackPhaseChanged -= OnPhase;
            body.StateChanged -= OnState;
            Tint(normal);
            Hide();
        }

        void OnPhase(BossMoveData move, AttackPhase phase)
        {
            switch (phase)
            {
                case AttackPhase.Windup: Tint(windup); Show(move); break;
                case AttackPhase.Active: Tint(active); break;
                case AttackPhase.Recovery: Tint(recovery); Hide(); break;
                case AttackPhase.Idle: Hide(); if (body.State == BossState.Idle) Tint(normal); break;
            }
        }

        void OnState(BossState state)
        {
            switch (state)
            {
                case BossState.Idle: Tint(normal); break;
                case BossState.Stunned: Tint(stunned); Hide(); break;
                case BossState.Dead: Tint(dead); Hide(); break;
            }
        }

        void Show(BossMoveData move)
        {
            if (move.FiresProjectile)
            {
                if (line == null) return;
                line.localScale = new Vector3(move.HitRadius * 2f, 0.02f, move.ProjectileRange);
                line.localPosition = new Vector3(0f, 0.02f, move.ProjectileRange * 0.5f);
                line.gameObject.SetActive(true);
            }
            else
            {
                if (disc == null) return;
                float diameter = move.HitRadius * 2f;
                disc.localScale = new Vector3(diameter, 0.01f, diameter);
                disc.localPosition = new Vector3(move.HitOffset.x, 0.02f, move.HitOffset.z);
                disc.gameObject.SetActive(true);
            }
        }

        void Hide()
        {
            if (disc != null) disc.gameObject.SetActive(false);
            if (line != null) line.gameObject.SetActive(false);
        }

        void Tint(Color color)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(LegacyColor, color);
            bodyRenderer.SetPropertyBlock(block);
        }
    }
}
