using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakPlunder.Audio
{
    /// <summary>
    /// 外部 AudioClip が未割当のとき、<see cref="AudioClip.Create"/> で手続き SE を生成する。
    /// CLAUDE.md Step 6 / GDD §15.2 の「アセット不要」方針に準拠。
    /// </summary>
    public static class ProceduralSoundClipFactory
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<SoundId, AudioClip> Cache = new();

        public static AudioClip Create(SoundId id)
        {
            if (id == SoundId.None) return null;
            if (Cache.TryGetValue(id, out var cached)) return cached;

            var clip = Generate(id);
            if (clip != null) Cache[id] = clip;
            return clip;
        }

        public static SoundLibrary.SoundEntry CreateEntry(SoundId id)
        {
            var clip = Create(id);
            if (clip == null) return null;

            return new SoundLibrary.SoundEntry
            {
                id             = id,
                clip           = clip,
                spatialBlend   = ClassifyIs2D(id) ? 0f : 1f,
                volume         = 1f,
                loop           = IsLoopSe(id),
                pitchVariation = HasPitchVariation(id) ? 0.05f : 0f,
                maxConcurrent  = DefaultConcurrency(id),
            };
        }

        private static AudioClip Generate(SoundId id)
        {
            string name = id.ToString();

            if (name.StartsWith("Footstep", StringComparison.Ordinal))
                return ToneNoiseBurst(name, 0.08f, 80f, 0.35f, 0.4f);

            if (name is "Jump")
                return SweepTone(name, 180f, 420f, 0.12f, 0.5f);

            if (name.StartsWith("Land", StringComparison.Ordinal))
                return Thump(name, name.Contains("Hard") ? 90f : 140f, name.Contains("Hard") ? 0.22f : 0.14f);

            if (name.StartsWith("Climb", StringComparison.Ordinal))
                return Click(name, name.Contains("Grab") ? 520f : 380f, 0.05f);

            if (name is "SlideFall" or "RagdollImpact")
                return NoiseBurst(name, 0.18f, 0.55f);

            if (name.StartsWith("Stamina", StringComparison.Ordinal))
                return PulseTone(name, 440f, 0.35f, 3f);

            if (name.StartsWith("Item", StringComparison.Ordinal))
                return name switch
                {
                    "ItemBreak"   => NoiseBurst(name, 0.2f, 0.7f),
                    "ItemImpact"  => Thump(name, 200f, 0.1f),
                    "ItemThrow"   => SweepTone(name, 300f, 120f, 0.1f, 0.35f),
                    _             => Click(name, 600f, 0.04f),
                };

            if (name.StartsWith("Rope", StringComparison.Ordinal) || name is "RopeSnap")
                return name switch
                {
                    "RopeTension" => PulseTone(name, 160f, 0.25f, 6f),
                    "RopeCut" or "RopeSnap" or "WinchCableSnap" => NoiseBurst(name, 0.15f, 0.65f),
                    _             => SweepTone(name, 800f, 200f, 0.12f, 0.45f),
                };

            if (name.StartsWith("Winch", StringComparison.Ordinal))
                return name == "WinchLoop"
                    ? LoopNoise(name, 1.5f, 0.12f)
                    : MotorTone(name, 90f, 0.25f);

            if (name is "IceAxeStrike")
                return MetallicClick(name, 900f, 0.08f);

            if (name is "AnchorBoltSet" or "BeltAttach")
                return Click(name, 350f, 0.06f);

            if (name.StartsWith("Grappling", StringComparison.Ordinal))
                return name.Contains("Fire")
                    ? SweepTone(name, 400f, 900f, 0.15f, 0.5f)
                    : Thump(name, 260f, 0.08f);

            if (name is "FoodEat")
                return Click(name, 280f, 0.05f);

            if (name is "FlareFire")
                return SweepTone(name, 200f, 1200f, 0.35f, 0.6f);

            if (name is "RadioActivate")
                return BeepSequence(name, new[] { 880f, 1100f }, 0.08f);

            if (name is "TentSetup" or "PackingWrap")
                return Rustle(name, 0.3f);

            if (name.StartsWith("Relic", StringComparison.Ordinal))
                return RelicSound(name);

            if (name.EndsWith("Ambient", StringComparison.Ordinal))
                return LoopNoise(name, 2f, name.Contains("Blizzard") ? 0.25f : 0.1f);

            if (name is "WindGust")
                return SweepNoise(name, 0.6f);

            if (name.StartsWith("Rockfall", StringComparison.Ordinal) || name is "Avalanche" or "FloorCrumble")
                return NoiseBurst(name, name.Contains("Warning") ? 0.4f : 0.55f, 0.75f);

            if (name is "IceCrack" or "FloorCrumbleWarn")
                return Crack(name, 0.12f);

            if (name.StartsWith("Trap", StringComparison.Ordinal))
                return name.Contains("Arrow") ? SweepTone(name, 600f, 100f, 0.2f, 0.5f) : Thump(name, 100f, 0.3f);

            if (name.StartsWith("Shrine", StringComparison.Ordinal))
                return Chime(name, name.Contains("Revive") ? 660f : 520f);

            if (name.StartsWith("Monster", StringComparison.Ordinal))
                return MonsterSound(name);

            if (name.StartsWith("Ui", StringComparison.Ordinal) || name.StartsWith("Result", StringComparison.Ordinal))
                return UiBeep(name);

            if (name.StartsWith("Heli", StringComparison.Ordinal))
                return name == "HeliHover" ? LoopNoise(name, 2f, 0.18f) : MotorTone(name, 70f, 0.5f);

            if (name is "WipeoutJingle")
                return BeepSequence(name, new[] { 523f, 392f, 262f }, 0.15f);

            if (name is "Checkpoint" or "Summit")
                return Fanfare(name);

            return Click(name, 440f, 0.05f);
        }

        private static AudioClip RelicSound(string name) => name switch
        {
            "RelicDiscover"     => Fanfare(name),
            "RelicGrab"         => Click(name, 500f, 0.05f),
            "RelicDamageLight"  => Click(name, 300f, 0.04f),
            "RelicDamageHeavy"  => Thump(name, 120f, 0.15f),
            "RelicDestroyed"    => NoiseBurst(name, 0.35f, 0.8f),
            "RelicDuckRoll"     => SweepTone(name, 200f, 80f, 0.2f, 0.4f),
            "RelicPotSing"      => ToneBurst(name, 330f, 0.4f),
            "RelicSphereHum"    => LoopTone(name, 220f, 1f),
            "RelicFishSlip"     => SweepTone(name, 400f, 150f, 0.15f, 0.35f),
            "RelicMagnetPull"   => PulseTone(name, 180f, 0.3f, 8f),
            "RelicTwinsChain"   => MetallicClick(name, 700f, 0.1f),
            _                   => Click(name, 440f, 0.05f),
        };

        private static AudioClip MonsterSound(string name) => name switch
        {
            "MonsterAlert"    => Roar(name, 80f, 0.4f),
            "MonsterChase"    => LoopNoise(name, 1.2f, 0.2f),
            "MonsterAttack"   => Thump(name, 60f, 0.2f),
            "MonsterFootstep" => Thump(name, 50f, 0.12f),
            "MonsterStunned"  => SweepTone(name, 200f, 60f, 0.3f, 0.5f),
            _                 => Roar(name, 70f, 0.3f),
        };

        private static AudioClip UiBeep(string name)
        {
            float freq = name.Contains("Fail") || name.Contains("Deny") ? 220f
                       : name.Contains("Approve") || name.Contains("Purchase") ? 880f
                       : 660f;
            return Click(name, freq, 0.06f);
        }

        private static AudioClip Click(string id, float freq, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.02f, 0.4f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip MetallicClick(string id, float freq, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.01f, 0.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f
                           + Mathf.Sin(2f * Mathf.PI * freq * 2.5f * t) * 0.2f;
                samples[i] = wave * env;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip Thump(string id, float freq, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.01f, 0.6f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.7f
                           + (UnityEngine.Random.value * 2f - 1f) * env * 0.15f;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip ToneNoiseBurst(string id, float duration, float freq, float noiseAmt, float amp)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.02f, 0.35f);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = UnityEngine.Random.value * 2f - 1f;
                samples[i] = (tone * (1f - noiseAmt) + noise * noiseAmt) * env * amp;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip NoiseBurst(string id, float duration, float amp)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float env = CalcEnvelope(i, count, 0.02f, 0.4f);
                samples[i] = (UnityEngine.Random.value * 2f - 1f) * env * amp;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip SweepTone(string id, float from, float to, float duration, float amp)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float freq = Mathf.Lerp(from, to, t);
                float env = CalcEnvelope(i, count, 0.05f, 0.3f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / SampleRate)) * env * amp;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip SweepNoise(string id, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float env = CalcEnvelope(i, count, 0.1f, 0.3f);
                samples[i] = (UnityEngine.Random.value * 2f - 1f) * env * 0.35f;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip PulseTone(string id, float freq, float duration, float pulseHz)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.02f, 0.15f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * pulseHz * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * pulse * 0.4f;
            }
            return BuildClip(id, samples, true);
        }

        private static AudioClip LoopTone(string id, float freq, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.2f;
            }
            return BuildClip(id, samples, true);
        }

        private static AudioClip LoopNoise(string id, float duration, float amp)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = (UnityEngine.Random.value * 2f - 1f) * amp;
            return BuildClip(id, samples, true);
        }

        private static AudioClip MotorTone(string id, float freq, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.05f, 0.2f);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f
                            + (UnityEngine.Random.value * 2f - 1f) * 0.1f) * env;
            }
            return BuildClip(id, samples, id.Contains("Loop") || id.Contains("Hover"));
        }

        private static AudioClip BeepSequence(string id, float[] freqs, float noteDur)
        {
            int perNote = Mathf.Max(1, Mathf.RoundToInt(SampleRate * noteDur));
            var samples = new float[perNote * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
            {
                for (int i = 0; i < perNote; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = CalcEnvelope(i, perNote, 0.05f, 0.25f);
                    samples[n * perNote + i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * 0.45f;
                }
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip Fanfare(string id)
            => BeepSequence(id, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f);

        private static AudioClip Chime(string id, float freq)
            => ToneBurst(id, freq, 0.5f);

        private static AudioClip ToneBurst(string id, float freq, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.05f, 0.35f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip Roar(string id, float freq, float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float env = CalcEnvelope(i, count, 0.05f, 0.25f);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f
                            + (UnityEngine.Random.value * 2f - 1f) * 0.35f) * env;
            }
            return BuildClip(id, samples, false);
        }

        private static AudioClip Rustle(string id, float duration)
            => NoiseBurst(id, duration, 0.25f);

        private static AudioClip Crack(string id, float duration)
            => NoiseBurst(id, duration, 0.45f);

        private static AudioClip BuildClip(string id, float[] samples, bool loop)
        {
            var clip = AudioClip.Create($"proc_{id}", samples.Length, 1, SampleRate, stream: false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float CalcEnvelope(int index, int total, float fadeInRatio, float fadeOutRatio)
        {
            float t = (float)index / total;
            float fade = 1f;
            if (t < fadeInRatio) fade = t / fadeInRatio;
            else if (t > 1f - fadeOutRatio) fade = (1f - t) / fadeOutRatio;
            return Mathf.Clamp01(fade);
        }

        private static bool ClassifyIs2D(SoundId id)
        {
            var s = id.ToString();
            return s.StartsWith("Ui", StringComparison.Ordinal)
                || s.StartsWith("Result", StringComparison.Ordinal)
                || s is "StaminaWarning" or "StaminaEmpty";
        }

        private static bool IsLoopSe(SoundId id)
        {
            var s = id.ToString();
            return s.EndsWith("Ambient", StringComparison.Ordinal)
                || s is "WinchLoop" or "StaminaWarning" or "HeliHover" or "MonsterChase";
        }

        private static bool HasPitchVariation(SoundId id)
        {
            var s = id.ToString();
            return s.StartsWith("Footstep", StringComparison.Ordinal)
                || s is "RagdollImpact"
                || s.StartsWith("Rockfall", StringComparison.Ordinal)
                || s is "ItemImpact";
        }

        private static int DefaultConcurrency(SoundId id)
        {
            var s = id.ToString();
            if (s.StartsWith("Footstep", StringComparison.Ordinal)) return 8;
            if (s.StartsWith("Ui", StringComparison.Ordinal)) return 2;
            if (s.EndsWith("Ambient", StringComparison.Ordinal)) return 1;
            return 4;
        }
    }
}
