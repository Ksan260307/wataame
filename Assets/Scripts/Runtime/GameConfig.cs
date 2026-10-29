using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// ゲーム全体の定数。寸法はメートル単位で Blender/generate_models.py のモデルと一致させている。
    /// </summary>
    public static class GameConfig
    {
        // わたあめ機 (原点 = 機械の足元の中心、正面 = -Z)
        public static readonly Vector3 HeadCenter = new Vector3(0f, 0.24f, 0f);
        public const float HeadRadius = 0.056f;
        public const float BowlBottomY = 0.178f;
        public const float BowlRimY = 0.40f;
        public const float BowlRimRadius = 0.30f;
        public const float MachineWidth = 0.68f;

        // ボウル内側のプロファイル (半径, 高さ)。Blender の inner と同じ
        static readonly Vector2[] BowlProfile =
        {
            new Vector2(0.10f, 0.178f), new Vector2(0.17f, 0.188f), new Vector2(0.23f, 0.215f),
            new Vector2(0.27f, 0.26f), new Vector2(0.292f, 0.32f), new Vector2(0.300f, 0.385f),
        };

        // 棒 (原点 = 先端)
        public const float StickLength = 0.42f;
        public const float StickRadius = 0.0055f;

        // わたあめの密度グリッド (棒のローカル座標)
        public const float CandyCellSize = 0.009f;
        public static readonly Vector3Int CandyGridPoints = new Vector3Int(41, 37, 41);
        public const float CandyGridBottom = 0.02f;
        public const float CandyDensityPerGram = 520f;

        // ざらめ
        public const float SugarMin = 5f;
        public const float SugarMax = 30f;
        public const float SugarDefault = 15f;
        public const float HopperCapacity = 40f;

        // 機械の挙動
        public const float FlossGramsPerSecond = 1.2f;
        public const float RaysPerGram = 250f;
        public const float HeatUpSeconds = 2.0f;
        public const float CoolDownSeconds = 4.0f;
        public const float SpinUpSeconds = 0.8f;
        public const float FlossRiseAboveRim = 0.08f;

        /// <summary>軸からの距離 r でのボウル内側の底面の高さ。ボウルの外はテーブル (0)。</summary>
        public static float BowlSurfaceY(float r)
        {
            if (r <= BowlProfile[0].x) return BowlBottomY;
            for (int i = 1; i < BowlProfile.Length; i++)
            {
                if (r <= BowlProfile[i].x)
                {
                    float t = Mathf.InverseLerp(BowlProfile[i - 1].x, BowlProfile[i].x, r);
                    return Mathf.Lerp(BowlProfile[i - 1].y, BowlProfile[i].y, t);
                }
            }
            return r <= BowlRimRadius + 0.02f ? BowlRimY : 0f;
        }

        /// <summary>高さ y でのボウル内側の半径。</summary>
        public static float BowlRadiusAt(float y)
        {
            if (y <= BowlProfile[0].y) return BowlProfile[0].x;
            for (int i = 1; i < BowlProfile.Length; i++)
            {
                if (y <= BowlProfile[i].y)
                {
                    float t = Mathf.InverseLerp(BowlProfile[i - 1].y, BowlProfile[i].y, y);
                    return Mathf.Lerp(BowlProfile[i - 1].x, BowlProfile[i].x, t);
                }
            }
            return BowlRimRadius;
        }
    }
}
