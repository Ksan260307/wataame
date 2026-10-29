using UnityEngine;

namespace Wataame
{
    /// <summary>プレイヤーの設定。PlayerPrefs に保存して次回も使う。</summary>
    public sealed class GameSettings
    {
        const string Prefix = "wataame.";

        /// <summary>操作の感度 (0.4 〜 2.0)。</summary>
        public float Sensitivity { get => sensitivity; set => sensitivity = Save("sensitivity", Mathf.Clamp(value, 0.4f, 2f)); }
        /// <summary>ベストゾーンへの吸い寄せアシスト。</summary>
        public bool Magnet { get => magnet; set => magnet = Save("magnet", value); }
        /// <summary>自動くるくるの速さ (度 / 秒)。</summary>
        public float SpinSpeed { get => spinSpeed; set => spinSpeed = Save("spinSpeed", Mathf.Clamp(value, 60f, 540f)); }
        /// <summary>回転の向き。true で右まわり (上から見て時計回り)。</summary>
        public bool Clockwise { get => clockwise; set => clockwise = Save("clockwise", value); }
        /// <summary>おまかせで回る円の半径 (m)。</summary>
        public float OrbitRadius { get => orbitRadius; set => orbitRadius = Save("orbitRadius", Mathf.Clamp(value, 0.12f, 0.24f)); }
        public int CameraPreset { get => cameraPreset; set => cameraPreset = Save("cameraPreset", value); }
        public bool ShowHelp { get => showHelp; set => showHelp = Save("showHelp", value); }

        float sensitivity, spinSpeed, orbitRadius;
        bool magnet, clockwise, showHelp;
        int cameraPreset;

        public GameSettings()
        {
            sensitivity = Load("sensitivity", 1f);
            magnet = Load("magnet", true);
            spinSpeed = Load("spinSpeed", 260f);
            clockwise = Load("clockwise", true);
            orbitRadius = Load("orbitRadius", 0.17f);
            cameraPreset = Load("cameraPreset", 0);
            showHelp = Load("showHelp", true);
        }

        static float Load(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);
        static int Load(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
        static bool Load(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;

        static float Save(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            return value;
        }

        static int Save(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            return value;
        }

        static bool Save(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            return value;
        }
    }
}
