using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace PeakPlunder.Audio
{
    /// <summary>
    /// GDD §15.2/15.3 — SE 定義テーブル ScriptableObject。
    /// AudioManager がロードし、SoundId → SoundEntry を辞書化して再生時に参照する。
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "PeakPlunder/Audio/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class SoundEntry
        {
            public SoundId id;
            public AudioClip clip;

            [Tooltip("0 = 2D / 1 = 3D (GDD §15.3)")]
            [Range(0f, 1f)] public float spatialBlend = 1f;

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("ループ再生 SE（wind_ambient/stamina_warning 等）")]
            public bool loop;

            [Tooltip("ランダム ±範囲ピッチ（0 = ピッチ変動なし、0.1 = ±10%）")]
            [Range(0f, 0.5f)] public float pitchVariation = 0.05f;

            [Tooltip("出力先 AudioMixerGroup（SE/Environment/UI 等）。未設定時は AudioManager のデフォルトグループ。")]
            public AudioMixerGroup mixerGroup;

            [Tooltip("最大同時発声数（同じ ID を短時間に連打しても max までしか重ならない）")]
            [Min(1)] public int maxConcurrent = 4;
        }

        [SerializeField] private SoundEntry[] _entries = Array.Empty<SoundEntry>();

        public IReadOnlyList<SoundEntry> Entries => _entries;

        /// <summary>
        /// SoundId → SoundEntry の辞書を構築する（AudioManager の起動時に一度だけ呼ぶ）。
        /// </summary>
        public Dictionary<SoundId, SoundEntry> BuildLookup()
        {
            var map = new Dictionary<SoundId, SoundEntry>(_entries.Length);
            foreach (var e in _entries)
            {
                if (e == null || e.id == SoundId.None) continue;
                if (map.ContainsKey(e.id))
                {
                    Debug.LogWarning($"[SoundLibrary] 重複 SoundId: {e.id} — 後続エントリは無視");
                    continue;
                }
                map.Add(e.id, e);
            }
            return map;
        }
    }
}
