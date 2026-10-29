using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// 機械のまわりを回るカメラ。視点プリセット (ななめ / まえ / うえ)、ズーム、棒を少し追いかける動きつき。
    /// できあがり表示ではわたあめに寄って自動で回る。縦長の画面では引いて全体が入るようにする。
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public struct Preset
        {
            public string Name;
            public float Pitch, Distance, TargetY;
        }

        public static readonly Preset[] Presets =
        {
            new Preset { Name = "ななめ", Pitch = 30f, Distance = 1.3f, TargetY = 0.33f },
            new Preset { Name = "まえ", Pitch = 10f, Distance = 1.15f, TargetY = 0.34f },
            new Preset { Name = "うえ", Pitch = 72f, Distance = 1.15f, TargetY = 0.28f },
        };

        const float MinZoom = 0.6f, MaxZoom = 1.6f;

        public Camera Camera { get; private set; }
        public int PresetIndex { get; private set; }
        public float ZoomFactor => zoom;

        /// <summary>画面の下側を UI が覆っている割合 (縦画面の操作パネル)。その分だけ 3D の表示を上に寄せる。</summary>
        public float BottomInset { get; set; }

        float yaw, pitch = Presets[0].Pitch, distance = Presets[0].Distance, zoom = 1f, pitchOffset;
        Vector3 target = new Vector3(0f, Presets[0].TargetY, 0f);
        float showcaseYaw;

        void Awake()
        {
            Camera = gameObject.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = WataameMaterials.SkyColor;
            Camera.fieldOfView = 38f;
            Camera.nearClipPlane = 0.02f;
            Camera.farClipPlane = 30f;
            gameObject.AddComponent<AudioListener>();
            gameObject.tag = "MainCamera";
            Apply();
        }

        public void SetPreset(int index)
        {
            PresetIndex = (index % Presets.Length + Presets.Length) % Presets.Length;
            yaw = 0f;
            zoom = 1f;
            pitchOffset = 0f;
        }

        public void NextPreset() => SetPreset(PresetIndex + 1);

        public void ResetView() => SetPreset(PresetIndex);

        public void Drive(GameInput input, bool showcase, Vector3 focus, float focusSize, Vector3 stickTip, float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 5f);
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-input.Zoom), MinZoom, MaxZoom);

            // 画面が縦長ほど引く (横幅に機械が収まるように)
            float aspect = Camera.pixelWidth / Mathf.Max(1f, (float)Camera.pixelHeight);
            float aspectFit = Mathf.Clamp(1.25f / Mathf.Max(aspect, 0.3f), 1f, 2.4f);

            if (showcase)
            {
                // できあがり: ドラッグでも回せる
                showcaseYaw += 18f * dt + (input.OrbitDrag.x + input.PointerDelta.x) * 0.3f;
                yaw = Mathf.LerpAngle(yaw, showcaseYaw, k);
                pitch = Mathf.Lerp(pitch, 15f, k);
                distance = Mathf.Lerp(distance, Mathf.Max(0.6f, focusSize * 5f) * aspectFit * zoom, k);
                target = Vector3.Lerp(target, focus, k);
            }
            else
            {
                var preset = Presets[PresetIndex];
                yaw += input.OrbitDrag.x * 0.3f;
                pitchOffset = Mathf.Clamp(preset.Pitch + pitchOffset - input.OrbitDrag.y * 0.3f, 5f, 85f) - preset.Pitch;
                pitch = Mathf.Lerp(pitch, preset.Pitch + pitchOffset, k);
                showcaseYaw = yaw;
                distance = Mathf.Lerp(distance, preset.Distance * aspectFit * zoom, k);
                // 棒が画面の外に出ないよう、少しだけ棒の方へ寄る
                Vector3 follow = new Vector3(stickTip.x * 0.3f, preset.TargetY + Mathf.Max(0f, stickTip.y - 0.45f) * 0.3f, stickTip.z * 0.3f);
                target = Vector3.Lerp(target, follow, k);
            }

            // 縦画面で下に操作パネルがあるときは、映像を上へずらしてパネルの上の範囲に収める
            // (投影行列をずらすので、タップ位置からのレイもそのまま正しく計算される)
            Camera.ResetProjectionMatrix();
            float inset = Mathf.Clamp(BottomInset, 0f, 0.6f);
            if (inset > 0f)
            {
                Matrix4x4 projection = Camera.projectionMatrix;
                projection[1, 2] = -inset;
                Camera.projectionMatrix = projection;
            }
            Apply();
        }

        void Apply()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }
    }
}
