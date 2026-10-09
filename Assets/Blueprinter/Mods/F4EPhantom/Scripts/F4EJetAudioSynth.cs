using UnityEngine;

namespace F4EPhantom
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class F4EJetAudioSynth : MonoBehaviour
    {
        [Range(0f, 1f)] public float throttle;
        [Range(0f, 1f)] public float afterburner;
        [SerializeField] private float idleFrequency = 74f;
        [SerializeField] private float militaryFrequency = 182f;

        private AudioSource source;
        private float phase;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.loop = true;
            source.clip = AudioClip.Create("F4E_J79_Procedural", 44100 * 2, 1, 44100, true, OnAudioRead);
            source.Play();
        }

        private void Update()
        {
            source.volume = Mathf.Lerp(0.06f, 0.90f, throttle) + afterburner * 0.10f;
            source.pitch = Mathf.Lerp(0.72f, 1.38f, throttle) + afterburner * 0.13f;
        }

        private void OnAudioRead(float[] data)
        {
            var frequency = Mathf.Lerp(idleFrequency, militaryFrequency, throttle) * (1f + afterburner * 0.34f);
            var increment = frequency / 44100f;
            for (var i = 0; i < data.Length; i++)
            {
                phase = (phase + increment) % 1f;
                var tonal = Mathf.Sin(phase * Mathf.PI * 2f) * 0.38f;
                var harmonic = Mathf.Sin(phase * Mathf.PI * 6f) * 0.17f;
                var noise = Mathf.PerlinNoise(phase * 491f, i * 0.013f) * 2f - 1f;
                data[i] = (tonal + harmonic + noise * (0.08f + afterburner * 0.17f)) * 0.45f;
            }
        }
    }
}
