using NUnit.Framework;
using UnityEngine;

namespace Wataame.Tests
{
    public class SugarHopperTests
    {
        [Test]
        public void AddRespectsCapacity()
        {
            var hopper = new SugarHopper(10f);
            Assert.AreEqual(8f, hopper.Add(8f, 0), 1e-4f);
            Assert.AreEqual(2f, hopper.Add(5f, 1), 1e-4f);
            Assert.AreEqual(0f, hopper.Add(1f, 1), 1e-4f);
            Assert.AreEqual(10f, hopper.Total, 1e-4f);
        }

        [Test]
        public void ConsumesInOrderOneColorAtATime()
        {
            var hopper = new SugarHopper(40f);
            hopper.Add(1f, 0);
            hopper.Add(2f, 3);

            Assert.AreEqual(1f, hopper.Consume(1.5f, out int first), 1e-4f);
            Assert.AreEqual(0, first);
            Assert.AreEqual(3, hopper.CurrentColorIndex);

            Assert.AreEqual(2f, hopper.Consume(5f, out int second), 1e-4f);
            Assert.AreEqual(3, second);
            Assert.IsTrue(hopper.IsEmpty);
            Assert.AreEqual(0f, hopper.Consume(1f, out int none));
            Assert.AreEqual(-1, none);
        }

        [Test]
        public void SameColorBatchesMerge()
        {
            var hopper = new SugarHopper(40f);
            hopper.Add(5f, 2);
            hopper.Add(5f, 2);
            Assert.AreEqual(1, hopper.Batches.Count);
            Assert.AreEqual(10f, hopper.Batches[0].Grams, 1e-4f);
        }
    }

    public class CandyFieldTests
    {
        static CandyField NewField() => new CandyField(GameConfig.CandyGridPoints, GameConfig.CandyCellSize,
            GameConfig.CandyGridBottom, GameConfig.StickRadius, GameConfig.CandyDensityPerGram);

        [Test]
        public void RayCatchesOnStick()
        {
            var field = NewField();
            var origin = new Vector3(-0.15f, 0.1f, 0f);
            Assert.IsTrue(field.Trace(origin, Vector3.right, 1f, out Vector3 hit));
            Assert.Less(Mathf.Abs(hit.x), 0.02f, "棒の近くで止まるはず");
        }

        [Test]
        public void RayMissesWhenPassingBesideStick()
        {
            var field = NewField();
            Assert.IsFalse(field.Trace(new Vector3(-0.15f, 0.1f, 0.05f), Vector3.right, 1f, out _));
        }

        [Test]
        public void DepositAccumulatesMassAndColor()
        {
            var field = NewField();
            var p = new Vector3(0.02f, 0.1f, 0f);
            for (int i = 0; i < 40; i++) field.Deposit(p, 0.01f, Color.red);
            Assert.AreEqual(0.4f, field.TotalGrams, 1e-4f);
            Assert.Greater(field.Sample(p), CandyField.Iso);
            Color c = field.SampleColor(p);
            Assert.Greater(c.r, 0.9f);
            Assert.Less(c.g, 0.1f);
        }

        [Test]
        public void ClearResetsEverything()
        {
            var field = NewField();
            field.Deposit(new Vector3(0f, 0.1f, 0f), 1f, Color.white);
            field.Clear();
            Assert.AreEqual(0f, field.TotalGrams);
            Assert.AreEqual(0f, field.Sample(new Vector3(0f, 0.1f, 0f)));
        }
    }

    public class SurfaceNetsTests
    {
        [Test]
        public void SphereProducesClosedOutwardMesh()
        {
            var field = new CandyField(new Vector3Int(33, 33, 33), 0.01f, 0f, 0.005f, 1f);
            Vector3 center = field.PointPosition(16, 16, 16);
            for (int z = 1; z < 32; z++)
            for (int y = 1; y < 32; y++)
            for (int x = 1; x < 32; x++)
            {
                float d = Vector3.Distance(field.PointPosition(x, y, z), center);
                field.Density[field.Index(x, y, z)] = Mathf.Max(0f, 2f - d / 0.1f); // 半径 0.1 で Iso
            }

            var nets = new SurfaceNets();
            nets.Build(field);
            Assert.Greater(nets.Triangles.Count, 300);

            // すべての三角形が外向き (Unity の表 = 時計回り)
            int inward = 0;
            for (int i = 0; i < nets.Triangles.Count; i += 3)
            {
                Vector3 a = nets.Vertices[nets.Triangles[i]], b = nets.Vertices[nets.Triangles[i + 1]], c = nets.Vertices[nets.Triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, (a + b + c) / 3f - center) <= 0f) inward++;
            }
            Assert.AreEqual(0, inward);

            // 頂点は球面の近く、法線は外向き
            for (int i = 0; i < nets.Vertices.Count; i++)
            {
                Vector3 offset = nets.Vertices[i] - center;
                Assert.AreEqual(0.1f, offset.magnitude, 0.012f);
                Assert.Greater(Vector3.Dot(nets.Normals[i], offset.normalized), 0.9f);
            }

            // 閉じたメッシュ: すべての辺がちょうど 2 つの三角形に共有される
            var edges = new System.Collections.Generic.Dictionary<long, int>();
            for (int i = 0; i < nets.Triangles.Count; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int u = nets.Triangles[i + k], v = nets.Triangles[i + (k + 1) % 3];
                long key = (long)Mathf.Min(u, v) << 32 | (uint)Mathf.Max(u, v);
                edges.TryGetValue(key, out int count);
                edges[key] = count + 1;
            }
            foreach (var count in edges.Values) Assert.AreEqual(2, count);
        }
    }

    public class CandyEvaluatorTests
    {
        [Test]
        public void SphereIsRound()
        {
            var field = new CandyField(GameConfig.CandyGridPoints, GameConfig.CandyCellSize, GameConfig.CandyGridBottom, GameConfig.StickRadius, 1f);
            var center = new Vector3(0f, 0.15f, 0f);
            for (int z = 1; z < field.NZ - 1; z++)
            for (int y = 1; y < field.NY - 1; y++)
            for (int x = 1; x < field.NX - 1; x++)
                if (Vector3.Distance(field.PointPosition(x, y, z), center) < 0.1f)
                    field.Density[field.Index(x, y, z)] = 2f;

            var grams = new float[CandyPalette.Count];
            grams[0] = 10f;
            var stats = CandyEvaluator.Evaluate(field, grams);
            Assert.Greater(stats.Roundness, 0.9f);
            Assert.AreEqual(20f, stats.WidthCm, 1.5f);
            StringAssert.StartsWith("ピンクの", stats.Name);
            StringAssert.EndsWith("くも", stats.Name);
        }

        [Test]
        public void TallShapeIsTower()
        {
            var field = new CandyField(GameConfig.CandyGridPoints, GameConfig.CandyCellSize, GameConfig.CandyGridBottom, GameConfig.StickRadius, 1f);
            for (int z = 1; z < field.NZ - 1; z++)
            for (int y = 1; y < field.NY - 1; y++)
            for (int x = 1; x < field.NX - 1; x++)
            {
                Vector3 p = field.PointPosition(x, y, z);
                if (p.x * p.x + p.z * p.z < 0.03f * 0.03f) field.Density[field.Index(x, y, z)] = 2f;
            }
            var stats = CandyEvaluator.Evaluate(field, new float[CandyPalette.Count]);
            StringAssert.EndsWith("タワー", stats.Name);
        }

        [Test]
        public void MixedColorsAreMix()
        {
            var field = new CandyField(GameConfig.CandyGridPoints, GameConfig.CandyCellSize, GameConfig.CandyGridBottom, GameConfig.StickRadius, 1f);
            for (int i = 0; i < field.Density.Length; i++)
                if (field.Density.Length / 2 - 200 < i && i < field.Density.Length / 2 + 200) field.Density[i] = 2f;
            var grams = new float[CandyPalette.Count];
            grams[0] = 5f;
            grams[1] = 5f;
            StringAssert.StartsWith("ミックス", CandyEvaluator.Evaluate(field, grams).Name);
        }
    }

    public class StickConstraintTests
    {
        static Vector3 Constrain(Vector3 p, Vector3 previous, float candyRadius = 0.0055f)
            => StickController.Constrain(p, previous, candyRadius, out _);

        [Test]
        public void CannotPassThroughBowlWall()
        {
            var inside = new Vector3(0.2f, 0.3f, 0f);
            var p = StickController.Constrain(new Vector3(0.3f, 0.3f, 0f), inside, 0.0055f, out var contact);
            Assert.LessOrEqual(new Vector2(p.x, p.z).magnitude, GameConfig.BowlRadiusAt(0.3f));
            Assert.IsTrue(contact.Wall);
        }

        [Test]
        public void PullingFarOutsideLiftsOverRim()
        {
            var inside = new Vector3(0.2f, 0.3f, 0f);
            var p = Constrain(new Vector3(0.5f, 0.3f, 0f), inside);
            Assert.Greater(p.y, GameConfig.BowlRimY);
            Assert.AreEqual(0.5f, p.x, 1e-4f);
        }

        [Test]
        public void LoweringFromOutsideStopsAtRim()
        {
            var outside = new Vector3(0.5f, 0.5f, 0f);
            var p = StickController.Constrain(new Vector3(0.5f, 0.3f, 0f), outside, 0.0055f, out var contact);
            Assert.AreEqual(GameConfig.BowlRimY, p.y, 1e-4f);
            Assert.IsTrue(contact.BlockedAtRim);
        }

        [Test]
        public void KeepsAwayFromHead()
        {
            var p = StickController.Constrain(new Vector3(0.01f, 0.24f, 0f), new Vector3(0.1f, 0.24f, 0f), 0.0055f, out var contact);
            Assert.GreaterOrEqual(new Vector2(p.x, p.z).magnitude, GameConfig.HeadRadius);
            Assert.IsTrue(contact.Head);
        }

        [Test]
        public void BigCandyStaysFurtherFromWall()
        {
            var inside = new Vector3(0.2f, 0.3f, 0f);
            var small = Constrain(new Vector3(0.27f, 0.3f, 0f), inside, 0.01f);
            var big = Constrain(new Vector3(0.27f, 0.3f, 0f), inside, 0.1f);
            Assert.Less(big.x, small.x);
        }

        [Test]
        public void BottomIsReported()
        {
            StickController.Constrain(new Vector3(0.15f, 0.1f, 0f), new Vector3(0.15f, 0.25f, 0f), 0.0055f, out var contact);
            Assert.IsTrue(contact.Bottom);
        }

        [Test]
        public void ZonesAreClassified()
        {
            Assert.AreEqual(StickController.Zone.Outside, StickController.Classify(new Vector3(0.2f, 0.6f, 0f)));
            Assert.AreEqual(StickController.Zone.Best, StickController.Classify(new Vector3(0.17f, 0.27f, 0f)));
            Assert.AreEqual(StickController.Zone.TooClose, StickController.Classify(new Vector3(0.08f, 0.27f, 0f)));
            Assert.AreEqual(StickController.Zone.Shallow, StickController.Classify(new Vector3(0.17f, 0.37f, 0f)));
        }

        [Test]
        public void BowlSurfaceFollowsProfile()
        {
            Assert.AreEqual(GameConfig.BowlBottomY, GameConfig.BowlSurfaceY(0.05f), 1e-4f);
            Assert.Greater(GameConfig.BowlSurfaceY(0.27f), GameConfig.BowlSurfaceY(0.2f));
            Assert.AreEqual(0f, GameConfig.BowlSurfaceY(0.5f), 1e-4f);
        }
    }

    public class AdviceTests
    {
        [Test]
        public void SmallCandyAsksForMoreSugar()
        {
            StringAssert.Contains("ざらめ", CandyEvaluator.Advise(new CandyStats { Grams = 2f, Roundness = 0.9f, Elongation = 1f }));
        }

        [Test]
        public void LopsidedCandyAsksToSpin()
        {
            StringAssert.Contains("くるくる", CandyEvaluator.Advise(new CandyStats { Grams = 10f, Offset = 0.5f, Roundness = 0.4f, Elongation = 1f }));
        }
    }
}
