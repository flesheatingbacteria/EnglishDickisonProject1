using UnityEngine;

namespace FlappyBird
{
    // Original instrumental variations inspired by the poem's singing bird.
    // Owned by the game, independent of bird physics and the death/recovery state.
    [RequireComponent(typeof(AudioSource))]
    public sealed class DickinsonMusic : MonoBehaviour
    {
        private AudioSource source, voice;
        // voice plays the melody alone, in step with the full theme, for when the music is hushed.
        private readonly AudioClip[] themes = new AudioClip[3], melodies = new AudioClip[3];
        private int level = -1, pendingLevel = -1;
        private float fade = 1, hush, melodyLevel = 1;
        private const float BaseVolume = 0.35f, FadeTime = 0.8f;
        public int CurrentBeat => source != null && source.isPlaying && !Muted && level >= 0
            ? Mathf.FloorToInt(source.time / BeatDuration(level)) : -1;
        private static float BeatDuration(int theme) => theme == 0 ? 0.48f : theme == 1 ? 0.6f : 0.68f;
        public float BeatLength => BeatDuration(level >= 0 ? level : 2);
        private float Clock => source != null && source.isPlaying ? source.time : Time.unscaledTime;
        // Seconds from the nearest beat of the playing theme (negative = early). Works while muted too.
        public float BeatError
        {
            get
            {
                float since = Mathf.Repeat(Clock, BeatLength);
                return since < BeatLength / 2 ? since : since - BeatLength;
            }
        }
        // 0 on the beat, rising to 1 just before the next.
        public float BeatPhase => Mathf.Repeat(Clock, BeatLength) / BeatLength;
        public bool Muted => source != null && source.mute;

        public void Initialize()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.volume = BaseVolume;
            source.ignoreListenerPause = true;
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = true;
            voice.spatialBlend = 0;
            voice.ignoreListenerPause = true;
            if (FindAnyObjectByType<AudioListener>() == null)
                Camera.main.gameObject.AddComponent<AudioListener>();
        }

        // Fades the current theme out before the new one starts.
        public void SetLevel(int index)
        {
            if (index == level && source.isPlaying) { pendingLevel = -1; return; }
            if (!source.isPlaying || level < 0) Switch(index);
            else pendingLevel = index;
        }

        private void Switch(int index)
        {
            level = index;
            if (themes[index] == null) themes[index] = Compose(index);
            source.clip = themes[index];
            source.Play();
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / FadeTime;
            if (pendingLevel >= 0)
            {
                fade -= step;
                if (fade <= 0) { fade = 0; Switch(pendingLevel); pendingLevel = -1; }
            }
            else fade = Mathf.Min(1, fade + step);
            source.volume = BaseVolume * fade * (1 - hush);
            if (hush > 0.001f && level >= 0)
            {
                Prepare(level);
                if (voice.clip != melodies[level] || !voice.isPlaying)
                {
                    voice.clip = melodies[level];
                    voice.Play();
                    voice.timeSamples = source.timeSamples;
                }
                voice.volume = BaseVolume * fade * hush * melodyLevel;
            }
            else if (voice.isPlaying) voice.Stop();
        }

        // hush 1 leaves only the bird's melody, played at melody (0 to 1).
        public void Hush(float amount, float melody = 1)
        {
            hush = Mathf.Clamp01(amount);
            melodyLevel = Mathf.Clamp01(melody);
        }

        // Builds a theme's melody-only clip ahead of time, so hushing never hitches.
        public void Prepare(int theme)
        {
            if (melodies[theme] == null) melodies[theme] = Compose(theme, true);
        }

        public void ToggleMute() { source.mute = voice.mute = !source.mute; }

        private static AudioClip Compose(int theme, bool melodyOnly = false)
        {
            const int sampleRate = 22050;
            float beat = BeatDuration(theme);
            const int beats = 48;
            int count = Mathf.RoundToInt(beats * beat * sampleRate);
            float[] samples = new float[count];
            int[] melody = { 0, 4, 7, 9, 7, 4, 2, 4, 7, 4, 2, 0,
                4, 7, 12, 9, 7, 4, 2, 7, 4, 2, 0, 0 };
            int[] roots = { 0, 0, 5, 5, 7, 7, 0, 0 };
            float tonic = theme == 0 ? 220f : theme == 1 ? 261.6256f : 196f;
            for (int note = 0; note < beats; note++)
            {
                int start = Mathf.RoundToInt(note * beat * sampleRate);
                int duration = Mathf.RoundToInt(beat * 1.8f * sampleRate);
                float pitch = tonic * Mathf.Pow(2, melody[note % melody.Length] / 12f);
                int root = roots[note / 6];
                float bass = tonic * 0.5f * Mathf.Pow(2, root / 12f);
                float harmony = bass * Mathf.Pow(2, (note % 3 == 0 ? 0 : note % 3 == 1 ? 7 : 12) / 12f);
                for (int j = 0; j < duration; j++)
                {
                    float t = j / (float)sampleRate;
                    float envelope = Mathf.Min(1, t / 0.025f) * Mathf.Exp(-t * (theme == 1 ? 3.8f : 2.8f))
                        * Mathf.Clamp01((duration - j) / (sampleRate * 0.08f));
                    float phase = 2 * Mathf.PI * pitch * t;
                    float birdSong = Mathf.Sin(phase + 0.018f * Mathf.Sin(t * 2 * Mathf.PI * 5))
                        + 0.2f * Mathf.Sin(phase * 2) + 0.07f * Mathf.Sin(phase * 3);
                    float accompaniment = melodyOnly ? 0 : Mathf.Sin(2 * Mathf.PI * harmony * t)
                        + (note % 3 == 0 ? 0.5f * Mathf.Sin(2 * Mathf.PI * bass * t) : 0);
                    // Wrap the ringing tail to make the loop seamless.
                    samples[(start + j) % count] += envelope * (birdSong * 0.13f + accompaniment * 0.065f);
                }
            }
            AudioClip clip = AudioClip.Create("Hope - " + new[] { "Gale", "Warmth", "Sea" }[theme] + (melodyOnly ? " (melody)" : ""), count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in themes) if (clip != null) Destroy(clip);
            foreach (AudioClip clip in melodies) if (clip != null) Destroy(clip);
        }
    }
}
