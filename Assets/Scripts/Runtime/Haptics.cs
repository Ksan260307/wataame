using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Wataame
{
    /// <summary>スマホの軽い振動。WebGL ではブラウザの navigator.vibrate を使う。</summary>
    public static class Haptics
    {
        static float lastPulse = -1f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void WataameVibrate(int milliseconds);
#endif

        public static void Pulse(int milliseconds = 15)
        {
            // 連続して鳴らしすぎない
            if (Time.unscaledTime - lastPulse < 0.12f) return;
            lastPulse = Time.unscaledTime;
#if UNITY_WEBGL && !UNITY_EDITOR
            WataameVibrate(milliseconds);
#elif UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
