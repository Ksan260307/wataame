using NUnit.Framework;
using UnityEngine;

namespace Wataame.Tests
{
    /// <summary>
    /// 機械と同じ方法で糸を飛ばし、棒を回しながら 20 g 分のわたあめを作ってみる。
    /// 実機の手触り (できる大きさ・受け止める割合) が大きく崩れていないかを確かめる。
    /// </summary>
    public class SimulationTests
    {
        static CandyStats Simulate(float sugarGrams, float spinDegPerSec, Vector3 tip, Vector3 tilt, out float caughtRatio)
        {
            Random.InitState(1234);
            var field = new CandyField(GameConfig.CandyGridPoints, GameConfig.CandyCellSize, GameConfig.CandyGridBottom,
                GameConfig.StickRadius, GameConfig.CandyDensityPerGram);
            const float dt = 1f / 60f;
            float remaining = sugarGrams, caught = 0f, radius = GameConfig.StickRadius, spin = 0f, sinceRemesh = 0f;

            while (remaining > 0f)
            {
                spin += spinDegPerSec * dt;
                Quaternion rot = Quaternion.Euler(tilt) * Quaternion.AngleAxis(spin, Vector3.up);
                Quaternion inv = Quaternion.Inverse(rot);
                Vector3 up = rot * Vector3.up;

                float grams = Mathf.Min(remaining, GameConfig.FlossGramsPerSecond * dt);
                remaining -= grams;
                int rays = Mathf.Max(1, Mathf.RoundToInt(grams * GameConfig.RaysPerGram));
                for (int i = 0; i < rays; i++)
                {
                    if (!FlossPhysics.Aim(GameConfig.HeadCenter, tip, up, radius, field.Size.y, out var shot)) continue;
                    Vector3 origin = inv * (shot.Origin - tip);
                    Vector3 dir = inv * shot.Direction;
                    if (field.Trace(origin, dir, shot.MaxDistance, out var hit))
                    {
                        field.Deposit(hit + Random.insideUnitSphere * field.Cell * 0.6f, grams / rays, Color.white);
                        caught += grams / rays;
                    }
                }

                sinceRemesh += dt;
                if (sinceRemesh > 0.06f)
                {
                    sinceRemesh = 0f;
                    radius = field.MeasureRadius();
                }
            }

            caughtRatio = caught / sugarGrams;
            var grams2 = new float[CandyPalette.Count];
            grams2[2] = caught;
            return CandyEvaluator.Evaluate(field, grams2);
        }

        [Test]
        public void SpinningStickMakesRoundCandy()
        {
            var stats = Simulate(20f, 260f, new Vector3(0.17f, 0.29f, 0f), Vector3.zero, out float ratio);
            Debug.Log($"spin: {stats.Name} {stats.Grams:0.0}g {stats.WidthCm:0.0}x{stats.HeightCm:0.0}cm round={stats.Roundness:0.00} elong={stats.Elongation:0.00} offset={stats.Offset:0.00} caught={ratio:P0}");
            Assert.Greater(ratio, 0.35f, "受け止める割合が低すぎる");
            Assert.Greater(stats.Grams, 7f);
            Assert.That(stats.WidthCm, Is.InRange(10f, 34f));
            Assert.Greater(stats.Roundness, 0.45f);
            Assert.Less(stats.Offset, 0.3f);
        }

        [Test]
        public void StillStickMakesLopsidedCandy()
        {
            var stats = Simulate(10f, 0f, new Vector3(0.17f, 0.29f, 0f), Vector3.zero, out float ratio);
            Debug.Log($"still: {stats.Name} {stats.Grams:0.0}g {stats.WidthCm:0.0}x{stats.HeightCm:0.0}cm round={stats.Roundness:0.00} elong={stats.Elongation:0.00} offset={stats.Offset:0.00} caught={ratio:P0}");
            Assert.Greater(stats.Offset, 0.25f, "回さないとヘッド側だけに付くはず");
        }

        [Test]
        public void StickAboveRimCatchesNothing()
        {
            Simulate(3f, 260f, new Vector3(0.17f, 0.62f, 0f), Vector3.zero, out float ratio);
            Assert.AreEqual(0f, ratio, 1e-4f);
        }
    }
}
