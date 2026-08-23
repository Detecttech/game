using System;
using UnityEngine;

namespace QuizBattle.Audio
{
    /// Self-contained audio manager with procedurally synthesized WebGL-compatible
    /// sound effects for attacks, freezes, victories, correct answers, and goofy wrong-answer buzzes.
    /// Works 100% in WebGL without needing external audio files or asset bundles.
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        private AudioSource _source;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // 2D clean stereo audio
            _source.volume = 0.85f;

            InitClips();
        }

        private AudioClip _correctClip;
        private AudioClip _wrongGoofyClip;
        private AudioClip _attackClip;
        private AudioClip _freezeClip;
        private AudioClip _bonusMoveClip;
        private AudioClip _victoryClip;
        private AudioClip _tickClip;

        private void InitClips()
        {
            _correctClip = SynthesizeTone(new[] { (523.25f, 0.08f), (659.25f, 0.08f), (783.99f, 0.16f) }, 0.32f, ToneType.SineHarmonics);
            _wrongGoofyClip = SynthesizeGoofyWahWah();
            _attackClip = SynthesizeAttackPunch();
            _freezeClip = SynthesizeFreezeChime();
            _bonusMoveClip = SynthesizeTone(new[] { (440f, 0.07f), (554.37f, 0.07f), (659.25f, 0.07f), (880f, 0.18f) }, 0.39f, ToneType.SquareChiptune);
            _victoryClip = SynthesizeVictoryFanfare();
            _tickClip = SynthesizeNoiseBurst(0.04f, 1200f);
        }

        public void PlayCorrect() => Play(_correctClip, 0.75f);
        public void PlayGoofyWrong() => Play(_wrongGoofyClip, 0.95f);
        public void PlayAttack() => Play(_attackClip, 0.85f);
        public void PlayFreeze() => Play(_freezeClip, 0.80f);
        public void PlayBonusMove() => Play(_bonusMoveClip, 0.80f);
        public void PlayVictory() => Play(_victoryClip, 0.95f);
        public void PlayTick() => Play(_tickClip, 0.50f);

        private void Play(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip != null && _source != null)
            {
                _source.PlayOneShot(clip, volumeScale);
            }
        }

        private enum ToneType { SineHarmonics, SquareChiptune, Noise }

        private static AudioClip SynthesizeTone((float freq, float duration)[] notes, float totalDuration, ToneType type)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * totalDuration);
            float[] samples = new float[totalSamples];

            int sampleOffset = 0;
            foreach (var (freq, duration) in notes)
            {
                int noteSamples = (int)(sampleRate * duration);
                for (int i = 0; i < noteSamples && (sampleOffset + i) < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env = Mathf.Sin(Mathf.Clamp01((float)i / noteSamples) * Mathf.PI); // Smooth envelope
                    float val = 0f;

                    if (type == ToneType.SineHarmonics)
                    {
                        val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f +
                              Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f +
                              Mathf.Sin(6f * Mathf.PI * freq * t) * 0.1f;
                    }
                    else if (type == ToneType.SquareChiptune)
                    {
                        val = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.5f;
                    }

                    samples[sampleOffset + i] = val * env * 0.65f;
                }
                sampleOffset += noteSamples;
            }

            var clip = AudioClip.Create("SynthesizedTone", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeGoofyWahWah()
        {
            // Comical downward slide with goofy vibrato & spring boing
            int sampleRate = 44100;
            float duration = 0.55f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float startFreq = 420f;
            float endFreq = 160f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Downward frequency sweep with comical wobble
                float currentFreq = Mathf.Lerp(startFreq, endFreq, Mathf.Pow(progress, 1.4f));
                float vibrato = Mathf.Sin(2f * Mathf.PI * 18f * t) * 25f;
                float freq = currentFreq + vibrato;

                // Envelope: punchy start then ringing fade
                float env = Mathf.Pow(1f - progress, 1.2f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + Mathf.Sign(Mathf.Sin(4f * Mathf.PI * freq * t)) * 0.2f;

                // Spring boing bounce near the end
                if (progress > 0.6f)
                {
                    float boingT = (progress - 0.6f) / 0.4f;
                    float boingFreq = 280f + Mathf.Sin(boingT * Mathf.PI * 8f) * 60f;
                    wave += Mathf.Sin(2f * Mathf.PI * boingFreq * t) * 0.4f * (1f - boingT);
                }

                samples[i] = Mathf.Clamp(wave * env * 0.8f, -1f, 1f);
            }

            var clip = AudioClip.Create("GoofyWahWah", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeAttackPunch()
        {
            // High energy whoosh followed by heavy thumping bass impact
            int sampleRate = 44100;
            float duration = 0.38f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Pitch envelope drops rapidly from 600Hz -> 65Hz (heavy punch impact)
                float freq = Mathf.Lerp(650f, 65f, Mathf.Pow(progress, 0.5f));
                float env = Mathf.Pow(1f - progress, 1.8f);

                float sin = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Max(0f, 1f - progress * 4f) * 0.4f;

                samples[i] = Mathf.Clamp((sin + noise) * env * 0.9f, -1f, 1f);
            }

            var clip = AudioClip.Create("AttackPunch", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeFreezeChime()
        {
            // Shimmering crystal FM ice chime
            int sampleRate = 44100;
            float duration = 0.48f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float[] freqs = { 880f, 1320f, 1760f, 2640f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float env = Mathf.Pow(1f - progress, 1.6f);

                float wave = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float fm = Mathf.Sin(2f * Mathf.PI * (freqs[f] * 2.01f) * t) * 0.3f;
                    wave += Mathf.Sin(2f * Mathf.PI * (freqs[f] + fm * 400f) * t) * (0.35f / (f + 1));
                }

                samples[i] = wave * env * 0.75f;
            }

            var clip = AudioClip.Create("FreezeChime", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeVictoryFanfare()
        {
            // Triumphant orchestral brass fanfare (C4 -> G4 -> C5 -> E5 chord)
            int sampleRate = 44100;
            float duration = 1.1f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            (float freq, float start, float len)[] brass = {
                (261.63f, 0.00f, 0.22f), // C4
                (392.00f, 0.18f, 0.22f), // G4
                (523.25f, 0.36f, 0.74f), // C5
                (659.25f, 0.36f, 0.74f), // E5
                (783.99f, 0.40f, 0.70f), // G5
            };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sum = 0f;

                foreach (var (freq, start, len) in brass)
                {
                    if (t >= start && t < start + len)
                    {
                        float localT = (t - start) / len;
                        float env = Mathf.Sin(localT * Mathf.PI);
                        float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f +
                                    Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f +
                                    Mathf.Sin(6f * Mathf.PI * freq * t) * 0.15f;
                        sum += val * env;
                    }
                }

                samples[i] = Mathf.Clamp(sum * 0.45f, -1f, 1f);
            }

            var clip = AudioClip.Create("VictoryFanfare", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeNoiseBurst(float duration, float cutoff)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float progress = (float)i / totalSamples;
                float env = 1f - progress;
                samples[i] = (UnityEngine.Random.value * 2f - 1f) * env * 0.5f;
            }

            var clip = AudioClip.Create("NoiseBurst", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
