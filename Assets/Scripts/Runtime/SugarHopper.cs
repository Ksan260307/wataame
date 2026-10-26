using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// わたあめ機に入っているざらめ。入れた順 (色ごと) に溶けていくので、
    /// 途中で別の色を足すと層になったわたあめが作れる。
    /// </summary>
    public sealed class SugarHopper
    {
        public struct Batch
        {
            public float Grams;
            public int ColorIndex;
        }

        readonly List<Batch> batches = new List<Batch>();
        readonly float capacity;

        public SugarHopper(float capacity)
        {
            this.capacity = capacity;
        }

        public IReadOnlyList<Batch> Batches => batches;
        public float Capacity => capacity;
        public float Total { get; private set; }
        public bool IsEmpty => Total <= 1e-4f;

        /// <summary>現在溶けている (先頭の) ざらめの色。空なら -1。</summary>
        public int CurrentColorIndex => batches.Count > 0 ? batches[0].ColorIndex : -1;

        /// <summary>ざらめを入れる。容量を超えた分は入らない。実際に入った量を返す。</summary>
        public float Add(float grams, int colorIndex)
        {
            float added = Mathf.Min(grams, capacity - Total);
            if (added <= 1e-4f) return 0f;

            int last = batches.Count - 1;
            if (last >= 0 && batches[last].ColorIndex == colorIndex)
                batches[last] = new Batch { Grams = batches[last].Grams + added, ColorIndex = colorIndex };
            else
                batches.Add(new Batch { Grams = added, ColorIndex = colorIndex });

            Total += added;
            return added;
        }

        /// <summary>
        /// 先頭のざらめを最大 grams だけ溶かす。1 回の呼び出しで消費するのは 1 色だけ。
        /// 実際に溶けた量を返す。
        /// </summary>
        public float Consume(float grams, out int colorIndex)
        {
            colorIndex = -1;
            if (batches.Count == 0 || grams <= 0f) return 0f;

            var head = batches[0];
            colorIndex = head.ColorIndex;
            float used = Mathf.Min(grams, head.Grams);
            head.Grams -= used;
            if (head.Grams <= 1e-4f)
                batches.RemoveAt(0);
            else
                batches[0] = head;

            Total = Mathf.Max(0f, Total - used);
            if (batches.Count == 0) Total = 0f;
            return used;
        }

        public void Clear()
        {
            batches.Clear();
            Total = 0f;
        }
    }
}
