using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MidiJack;   // Needed for MidiMaster

[System.Serializable]
public class MidiHitZone
{
    public int midiNote;
    public GameObject hitZone;
    public ParticleSystem hitParticles;

    // Use one AudioSource per zone to play clips
    public AudioSource hitAudio;

    // Velocity-layered samples: index 0=soft, 1=medium, 2=hard...
    public AudioClip[] velocityClips;

    // Per-note emission color
    public Color emissionColor = Color.white;

    [HideInInspector] public List<Material> runtimeMaterials = new List<Material>();
    [HideInInspector] public Coroutine activePulse = null;
}

public class MidiHit : MonoBehaviour
{
    public List<MidiHitZone> mappings = new List<MidiHitZone>();

    public float hitVisualDuration = 0.5f;
    public float velocityToEmission = 2.0f;
    // Per-hit multiplier to push emission into a stronger HDR range
    public float emissionBoost = 8.0f;
    public float globalLatencyOffset = 0f;

    Dictionary<int, MidiHitZone> mapLookup;

    void Awake()
    {
        mapLookup = new Dictionary<int, MidiHitZone>();

        foreach (var m in mappings)
        {
            if (m.hitZone == null) continue;

            // Clone materials so each hitZone glows independently
            var renderers = m.hitZone.GetComponentsInChildren<Renderer>();
            m.runtimeMaterials.Clear();

            foreach (var rend in renderers)
            {
                Material inst = new Material(rend.sharedMaterial);
                // Ensure emission keyword and realtime emissive GI flag for stronger/emissive behaviour
                inst.EnableKeyword("_EMISSION");
                inst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                // Clear any existing emission so runtime starts dark
                if (inst.HasProperty("_EmissionColor"))
                    inst.SetColor("_EmissionColor", Color.black);

                rend.material = inst;
                m.runtimeMaterials.Add(inst);
            }

            if (!mapLookup.ContainsKey(m.midiNote))
                mapLookup.Add(m.midiNote, m);
        }

        MidiMaster.noteOnDelegate += OnMidiNoteOn;
    }

    void OnDestroy()
    {
        MidiMaster.noteOnDelegate -= OnMidiNoteOn;
    }

    void OnMidiNoteOn(MidiChannel ch, int note, float velocity)
    {
        Debug.Log($"MIDI note received: {note} vel:{velocity}");

        float vel = velocity;
        if (vel > 1f) vel = Mathf.Clamp01(velocity / 127f);

        if (Mathf.Abs(globalLatencyOffset) > 0f)
            StartCoroutine(TriggerHitAfterDelay(note, vel, globalLatencyOffset));
        else
            TriggerHit(note, vel);
    }

    IEnumerator TriggerHitAfterDelay(int note, float vel, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        TriggerHit(note, vel);
    }

     public void TriggerHit(int note, float velocity01)
    {
        if (!mapLookup.TryGetValue(note, out var mz)) return;

        if (mz.hitParticles != null) mz.hitParticles.Play();

        // Pick clip by velocity and play it
        PlayVelocityClip(mz, velocity01);

        if (mz.activePulse != null)
            StopCoroutine(mz.activePulse);

        mz.activePulse = StartCoroutine(PulseEmissionForMaterials(mz.runtimeMaterials, velocity01, mz));
    }

    void PlayVelocityClip(MidiHitZone mz, float velocity01)
    {
        if (mz.hitAudio == null || mz.velocityClips == null || mz.velocityClips.Length == 0) return;

        // Map velocity to layer index
        int idx = 0;
        if (mz.velocityClips.Length == 1) idx = 0;
        else
        {
            // Evenly distribute 0..1 across array length
            float scaled = velocity01 * mz.velocityClips.Length;
            idx = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, mz.velocityClips.Length - 1);
        }

        var clip = mz.velocityClips[idx];
        if (clip == null) return;

        // Optional: scale volume minimally by velocity, main dynamics come from clip selection
        mz.hitAudio.volume = Mathf.Lerp(0.7f, 1f, velocity01);
        mz.hitAudio.pitch = 1f; // keep pitch fixed for drums

        // PlayOneShot avoids interrupting overlapping hits
        mz.hitAudio.PlayOneShot(clip, 1f);
    }
    IEnumerator PulseEmissionForMaterials(List<Material> mats, float velocity01, MidiHitZone zoneRef)
    {
        // Use HDR emission so it shows up even on bright albedo.
        // Tune velocityToEmission and emissionBoost in the inspector to increase visibility.
        float maxIntensity = Mathf.Lerp(1f, velocityToEmission * emissionBoost, velocity01);

        foreach (var mat in mats)
        {
            mat.EnableKeyword("_EMISSION");
            // ensure realtime emissive flag
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // Use full configured color (linear) and scale by HDR intensity
            Color emission = zoneRef.emissionColor.linear * maxIntensity;
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emission);
        }

        yield return new WaitForSeconds(hitVisualDuration);

        float t = 0f;
        float fadeDur = hitVisualDuration;

        while (t < fadeDur)
        {
            t += Time.deltaTime;
            float f = 1f - (t / fadeDur);

            foreach (var mat in mats)
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    Color emission = zoneRef.emissionColor.linear * (maxIntensity * f);
                    mat.SetColor("_EmissionColor", emission);
                }
            }
            yield return null;
        }

        foreach (var mat in mats)
        {
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);
        }

        zoneRef.activePulse = null;
    }
}
