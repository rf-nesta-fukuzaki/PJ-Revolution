using PeakPlunder.Audio;
using UnityEngine;

namespace PeakPlunder.Audio
{
    /// <summary>
    /// タイトル / ショップ用の手続き BGM（外部 AudioClip 不要）。
    /// </summary>
    public static class MenuAmbientBgmFactory
    {
        private const int SampleRate = 44100;

        private static AudioClip s_titleClip;
        private static AudioClip s_shopClip;

        public static AudioClip GetTitleBgm()
        {
            s_titleClip ??= CreateAmbientLoop("MenuBgm_Title", 0.22f,
                new[] { 110f, 165f, 220f }, new[] { 0.35f, 0.25f, 0.15f });
            return s_titleClip;
        }

        public static AudioClip GetShopBgm()
        {
            s_shopClip ??= CreateAmbientLoop("MenuBgm_Shop", 0.20f,
                new[] { 98f, 147f, 196f }, new[] { 0.3f, 0.28f, 0.12f });
            return s_shopClip;
        }

        private static AudioClip CreateAmbientLoop(string name, float volume,
            float[] freqs, float[] amps)
        {
            const float duration = 8f;
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;
                for (int f = 0; f < freqs.Length; f++)
                    sample += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) * amps[f];

                // 緩やかな LFO で山の静けさを演出
                float lfo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.08f * t);
                sample *= lfo * volume;
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
