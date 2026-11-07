using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    internal enum Cue { Tick, Warning, End, Overtime }

    // Timer sounds are synthesized at startup so the mod ships as a single DLL with no audio assets
    internal sealed class Beeper : MonoBehaviour
    {
        const int SampleRate = 44100;

        CastSettings settings;
        AudioSource source;
        AudioClip tick, warning, end, overtime;

        public void Init(CastSettings settings)
        {
            this.settings = settings;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.bypassReverbZones = true;

            tick = Tone("cast_tick", new[] { 1046f }, 0.09f);
            warning = Tone("cast_warning", new[] { 880f, 1318f }, 0.32f);
            end = Tone("cast_end", new[] { 523f, 392f }, 0.9f);
            overtime = Tone("cast_overtime", new[] { 659f, 880f, 1046f }, 0.45f);
        }

        public void Play(Cue cue)
        {
            if (source == null || settings.soundVolume <= 0f) return;
            AudioClip clip = cue == Cue.Tick ? tick : cue == Cue.Warning ? warning : cue == Cue.End ? end : overtime;
            source.PlayOneShot(clip, settings.soundVolume);
        }

        // Plays each frequency in sequence with a short attack/decay envelope so it doesn't click
        static AudioClip Tone(string name, float[] freqs, float seconds)
        {
            int total = Mathf.CeilToInt(seconds * SampleRate);
            int segment = total / freqs.Length;
            var data = new float[total];
            for (int i = 0; i < total; i++)
            {
                int seg = Mathf.Min(i / segment, freqs.Length - 1);
                float local = (i - seg * segment) / (float)SampleRate;
                float segLen = segment / (float)SampleRate;
                float attack = Mathf.Clamp01(local / 0.008f);
                float decay = Mathf.Clamp01((segLen - local) / (segLen * 0.6f));
                float t = i / (float)SampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * freqs[seg] * t) * 0.8f + Mathf.Sin(4f * Mathf.PI * freqs[seg] * t) * 0.2f;
                data[i] = wave * attack * decay * 0.5f;
            }
            AudioClip clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (tick != null) Destroy(tick);
            if (warning != null) Destroy(warning);
            if (end != null) Destroy(end);
            if (overtime != null) Destroy(overtime);
        }
    }
}
