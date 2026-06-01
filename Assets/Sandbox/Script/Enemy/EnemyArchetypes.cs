using UnityEngine;

/// <summary>
/// 各 <see cref="EnemyArchetype"/> のチューニング済み設定と見た目を生成するファクトリ。
/// EnemySpawner から呼び出してバリエーション豊かな敵を配置する。
/// </summary>
public static class EnemyArchetypes
{
    public struct Preset
    {
        public EnemyConfigSO Config;
        public Color BodyColor;
        public Color EyeColor;
        public float BodyHeight;
        public float BodyWidth;
    }

    public static Preset Build(EnemyArchetype archetype)
    {
        var cfg = ScriptableObject.CreateInstance<EnemyConfigSO>();
        cfg.Archetype = archetype;

        switch (archetype)
        {
            case EnemyArchetype.Stalker:
                cfg.VisionRange = 30f; cfg.VisionFov = 130f; cfg.HearingRadius = 18f;
                cfg.PatrolSpeed = 3.0f; cfg.ChaseSpeed = 6.4f; cfg.TurnSpeed = 11f;
                cfg.AttackRange = 1.9f; cfg.AttackDamage = 16f; cfg.AttackCooldown = 1.1f;
                cfg.LoseTargetGrace = 2.5f; cfg.SearchDuration = 8f;
                return new Preset {
                    Config = cfg,
                    BodyColor = new Color(0.18f, 0.04f, 0.05f),
                    EyeColor  = new Color(1f, 0.05f, 0.0f),
                    BodyHeight = 2.2f, BodyWidth = 0.85f,
                };

            case EnemyArchetype.Listener:
                cfg.VisionRange = 6f; cfg.VisionFov = 160f; cfg.HearingRadius = 42f;
                cfg.CrouchVisionFactor = 0.5f;
                cfg.PatrolSpeed = 2.4f; cfg.ChaseSpeed = 5.2f; cfg.TurnSpeed = 8f;
                cfg.AttackRange = 2.2f; cfg.AttackDamage = 24f; cfg.AttackCooldown = 1.4f;
                cfg.InvestigateDuration = 7f; cfg.SearchDuration = 9f; cfg.LoseTargetGrace = 2f;
                return new Preset {
                    Config = cfg,
                    BodyColor = new Color(0.72f, 0.70f, 0.66f),
                    EyeColor  = new Color(0.1f, 0.6f, 1f),
                    BodyHeight = 2.5f, BodyWidth = 1.2f,
                };

            case EnemyArchetype.Brute:
            default:
                cfg.VisionRange = 18f; cfg.VisionFov = 100f; cfg.HearingRadius = 14f;
                cfg.PatrolSpeed = 2.0f; cfg.ChaseSpeed = 4.6f; cfg.TurnSpeed = 7f;
                cfg.AttackRange = 2.4f; cfg.AttackDamage = 34f; cfg.AttackCooldown = 1.6f;
                cfg.LootKnockImpulse = 8f;
                return new Preset {
                    Config = cfg,
                    BodyColor = new Color(0.10f, 0.08f, 0.13f),
                    EyeColor  = new Color(1f, 0.2f, 0.05f),
                    BodyHeight = 2.8f, BodyWidth = 1.35f,
                };
        }
    }
}
