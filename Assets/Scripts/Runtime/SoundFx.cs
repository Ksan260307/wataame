using UnityEngine;

namespace Wataame
{
    /// <summary>効果音。音声ファイルは使わず、起動時に波形を合成する。</summary>
    public sealed class SoundFx : MonoBehaviour
    {
        const int SampleRate = 22050;

        AudioSource motor;
        AudioSource oneShot;
        AudioClip click, pour, done;

        void Awake()
        {
            motor = gameObject.AddComponent<AudioSource>();
            motor.clip = Synthesize("Motor", 1f, MotorWave);
            motor.loop = true;
            motor.volume = 0f;
            motor.playOnAwake = false;

            oneShot = gameObject.AddComponent<AudioSource>();
            oneShot.playOnAwake = false;

            click = Synthesize("Click", 0.06f, t => Mathf.Sin(t * 2f * Mathf.PI * 1800f) * Mathf.Exp(-t * 90f));
            pour = Synthesize("Pour", 0.6f, t => (Random.value * 2f - 1f) * 0.35f * Mathf.Sin(Mathf.PI * t / 0.6f) * (0.6f + 0.4f * Mathf.Sin(t * 90f)));
            done = Synthesize("Done", 0.7f, t =>
            {
                float f = t < 0.18f ? 784f : t < 0.36f ? 988f : 1175f;
                return Mathf.Sin(t * 2f * Mathf.PI * f) * 0.3f * Mathf.Exp(-(t % 0.18f) * 6f) * Mathf.Clamp01((0.7f - t) * 5f);
            });
        }

        public void SetMotor(float spin, float heat)
        {
            if (motor == null) return;
            float volume = spin * 0.22f;
            if (volume > 0.001f && !motor.isPlaying) motor.Play();
            else if (volume <= 0.001f && motor.isPlaying) motor.Stop();
            motor.volume = volume;
            motor.pitch = 0.55f + 0.5f * spin + 0.05f * heat;
        }

        public void PlayClick() => oneShot.PlayOneShot(click, 0.6f);
        public void PlayPour() => oneShot.PlayOneShot(pour, 0.5f);
        public void PlayDone() => oneShot.PlayOneShot(done, 0.6f);

        static float MotorWave(float t)
        {
            // 1 秒ちょうどでループする整数周波数の和 + 少しのノイズ
            return Mathf.Sin(t * 2f * Mathf.PI * 110f) * 0.45f
                   + Mathf.Sin(t * 2f * Mathf.PI * 220f) * 0.2f
                   + Mathf.Sin(t * 2f * Mathf.PI * 330f) * 0.08f
                   + (Random.value * 2f - 1f) * 0.06f;
        }

        static AudioClip Synthesize(string name, float seconds, System.Func<float, float> wave)
        {
            int length = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[length];
            for (int i = 0; i < length; i++) data[i] = wave((float)i / SampleRate);
            var clip = AudioClip.Create(name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
