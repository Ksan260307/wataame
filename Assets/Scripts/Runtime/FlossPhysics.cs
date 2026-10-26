using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// ヘッドから飛び出す糸 1 本の向きを決める。
    /// 糸はボウルの中をほぼ水平に漂ってくるので、棒のある高さごとにヘッド側から飛ばし、
    /// わたあめの大きさに応じて左右にばらつかせる。縁より上は糸が薄くなる。
    /// </summary>
    public static class FlossPhysics
    {
        const float SubmergedForFullCatch = 0.1f;

        public struct Shot
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public float MaxDistance;
        }

        /// <param name="headCenter">ヘッド中心 (ワールド)</param>
        /// <param name="stickTip">棒の先端 (ワールド)</param>
        /// <param name="stickUp">棒の向き (先端 → 持ち手)</param>
        /// <param name="candyRadius">棒の軸からわたあめ表面までの半径</param>
        /// <param name="gridHeight">わたあめグリッドの高さ</param>
        public static bool Aim(Vector3 headCenter, Vector3 stickTip, Vector3 stickUp, float candyRadius, float gridHeight, out Shot shot)
        {
            shot = default;
            float baseY = headCenter.y - GameConfig.HeadCenter.y;
            if (stickUp.y < 0.1f) return false;

            // 棒のうち、糸が漂っている高さ (ボウルの底 〜 縁の少し上) にある区間だけをねらう
            float yMin = baseY + GameConfig.BowlBottomY + 0.01f;
            float yMax = baseY + GameConfig.BowlRimY + GameConfig.FlossRiseAboveRim;
            float tMin = Mathf.Max(GameConfig.CandyGridBottom + 0.01f, (yMin - stickTip.y) / stickUp.y);
            float tMax = Mathf.Min(GameConfig.CandyGridBottom + gridHeight - 0.01f, (yMax - stickTip.y) / stickUp.y);
            if (tMax <= tMin) return false;
            // 少しだけ浸けたときは受け止める量も少ない
            if (Random.value > (tMax - tMin) / SubmergedForFullCatch) return false;

            float t = Random.Range(tMin, tMax);
            Vector3 target = stickTip + stickUp * t;
            float localY = target.y - baseY;
            if (localY > GameConfig.BowlRimY)
            {
                float fade = (localY - GameConfig.BowlRimY) / GameConfig.FlossRiseAboveRim;
                if (Random.value < fade) return false;
            }

            Vector3 flat = target - headCenter;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist < 1e-3f) return false;

            float spread = Mathf.Atan2(candyRadius * 1.15f + 0.006f, dist) * Mathf.Rad2Deg;
            Vector3 radial = Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * (flat / dist);

            Vector3 origin = headCenter + radial * GameConfig.HeadRadius;
            origin.y = target.y + Random.Range(-0.008f, 0.008f);
            shot.Origin = origin;
            shot.Direction = (radial + Vector3.up * Random.Range(-0.04f, 0.04f)).normalized;

            // ボウルの壁まで飛ぶ
            float wallY = Mathf.Clamp(localY, GameConfig.BowlBottomY, GameConfig.BowlRimY);
            float horizontal = new Vector2(shot.Direction.x, shot.Direction.z).magnitude;
            shot.MaxDistance = (GameConfig.BowlRadiusAt(wallY) - GameConfig.HeadRadius) / Mathf.Max(0.5f, horizontal);
            return true;
        }
    }
}
