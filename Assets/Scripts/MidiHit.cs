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

    // Per-note emission color
    public Color emissionColor = Color.white;

    [HideInInspector] public List<Material> runtimeMaterials = new List<Material>();
    [HideInInspector] public Coroutine activePulse = null;
}

public class MidiHit : MonoBehaviour
{
    public List<MidiHitZone> mappings = new List<MidiHitZone>();

    [Header("Flash Safety")]
    [Range(0.05f, 0.4f)] public float hitVisualDuration = 0.18f;
    [Range(0.1f, 2f)] public float velocityToEmission = 1.0f;
    [Range(0f, 1f)] public float minimumEmissionIntensity = 0.2f;
    [Range(0.2f, 3f)] public float emissionBoost = 1.1f;
    [Range(0.2f, 3f)] public float maxEmissionIntensity = 1.4f;
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

            // Extra logging for toms
            if (m.midiNote == 48 || m.midiNote == 45 || m.midiNote == 43)
            {
                Debug.Log($"[MidiHit] TOM SETUP: MIDI {m.midiNote} → {m.hitZone.name}, renderers={renderers.Length}");
            }

            foreach (var rend in renderers)
            {
                Material inst = new Material(rend.sharedMaterial);
                // Ensure emission keyword and realtime emissive GI flag for stronger/emissive behaviour
                inst.EnableKeyword("_EMISSION");
                inst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                // For URP Lit shader, also set the emission map to the base texture if not set
                if (inst.shader.name.Contains("Universal Render Pipeline/Lit") || inst.shader.name.Contains("URP/Lit"))
                {
                    if (inst.HasProperty("_EmissionMap") && inst.GetTexture("_EmissionMap") == null)
                    {
                        // Use base texture as emission map if available
                        if (inst.HasProperty("_BaseMap"))
                        {
                            Texture baseTex = inst.GetTexture("_BaseMap");
                            if (baseTex != null)
                            {
                                inst.SetTexture("_EmissionMap", baseTex);
                            }
                        }
                    }
                }

                // Clear any existing emission so runtime starts dark
                if (inst.HasProperty("_EmissionColor"))
                {
                    inst.SetColor("_EmissionColor", Color.black);
                }
                else if (m.midiNote == 48 || m.midiNote == 45 || m.midiNote == 43)
                {
                    Debug.LogWarning($"[MidiHit] TOM {m.midiNote}: Material '{inst.name}' doesn't support _EmissionColor! Shader: {inst.shader.name}");
                }

                rend.material = inst;
                m.runtimeMaterials.Add(inst);
                
                if (m.midiNote == 48 || m.midiNote == 45 || m.midiNote == 43)
                {
                    Debug.Log($"[MidiHit] TOM {m.midiNote}: Added material '{inst.name}', shader={inst.shader.name}, hasEmission={inst.HasProperty("_EmissionColor")}");
                }
            }

            if (!mapLookup.ContainsKey(m.midiNote))
                mapLookup.Add(m.midiNote, m);
        }

        MidiMaster.noteOnDelegate += OnMidiNoteOn;
        
        ValidateMappings();
    }
    
    void ValidateMappings()
    {
        // Expected MIDI notes for a standard drum kit
        int[] expectedNotes = new int[] 
        { 
            36,  // Bass Drum
            49,  // Crash
            42,  // Hi-Hat Closed
            38,  // Snare
            48,  // Tom 1 (High Tom)
            45,  // Tom 2 (Mid Tom)
            43,  // Tom 3 (Floor Tom)
            51   // Ride
        };
        
        Debug.Log("[MidiHit] ===== MIDI MAPPING VALIDATION =====");
        Debug.Log($"[MidiHit] Found {mapLookup.Count} MIDI note mappings");
        
        foreach (var note in expectedNotes)
        {
            if (mapLookup.ContainsKey(note))
            {
                var zone = mapLookup[note];
                string zoneName = zone.hitZone != null ? zone.hitZone.name : "NULL";
                Debug.Log($"[MidiHit] ✓ MIDI {note} → {zoneName}");
            }
            else
            {
                string noteName = GetDrumNoteName(note);
                Debug.LogWarning($"[MidiHit] ✗ MISSING: MIDI {note} ({noteName}) - Add this to MidiHit mappings!");
            }
        }
        
        // Check for duplicate or extra mappings
        foreach (var kvp in mapLookup)
        {
            if (System.Array.IndexOf(expectedNotes, kvp.Key) == -1)
            {
                Debug.Log($"[MidiHit] ! Extra mapping: MIDI {kvp.Key} → {kvp.Value.hitZone?.name}");
            }
        }
        
        Debug.Log("[MidiHit] ====================================");
    }
    
    string GetDrumNoteName(int midiNote)
    {
        switch (midiNote)
        {
            case 36: return "Bass Drum";
            case 49: return "Crash";
            case 42: return "Hi-Hat Closed";
            case 21:
            case 23:
            case 44:
            case 46: return "Hi-Hat";
            case 38: return "Snare";
            case 40: return "Snare Rim";
            case 48: return "Tom 1 (High Tom)";
            case 50: return "Tom 1 Alt";
            case 45: return "Tom 2 (Mid Tom)";
            case 47: return "Tom 2 Alt";
            case 43: return "Tom 3 (Floor Tom)";
            case 58: return "Tom 3 Alt";
            case 51: return "Ride";
            default: return "Unknown";
        }
    }

    void OnDestroy()
    {
        MidiMaster.noteOnDelegate -= OnMidiNoteOn;
    }

    void OnMidiNoteOn(MidiChannel ch, int note, float velocity)
    {
        // Extra logging for toms
        if (note == 48 || note == 45 || note == 43)
        {
            Debug.Log($"[MidiHit] TOM MIDI RECEIVED: note={note} vel={velocity}");
        }
        else
        {
            Debug.Log($"MIDI note received: {note} vel:{velocity}");
        }

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
        if (!mapLookup.TryGetValue(note, out var mz))
        {
            Debug.LogWarning($"[MidiHit] No mapping found for MIDI note {note}");
            return;
        }

        // Extra logging for toms
        if (note == 48 || note == 45 || note == 43)
        {
            Debug.Log($"[MidiHit] TOM HIT! MIDI {note} → {mz.hitZone?.name}, velocity={velocity01:F2}, runtimeMaterials={mz.runtimeMaterials.Count}, emissionColor={mz.emissionColor}");
        }

        if (mz.hitParticles != null) mz.hitParticles.Play();

        // Stop previous pulse and reset emission to black before starting new one
        if (mz.activePulse != null)
        {
            StopCoroutine(mz.activePulse);
            mz.activePulse = null;
            
            // Force reset emission to black to prevent stuck glow
            foreach (var mat in mz.runtimeMaterials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        mz.activePulse = StartCoroutine(PulseEmissionForMaterials(mz.runtimeMaterials, velocity01, mz));
    }

    IEnumerator PulseEmissionForMaterials(List<Material> mats, float velocity01, MidiHitZone zoneRef)
    {
        float targetIntensity = Mathf.Lerp(minimumEmissionIntensity, velocityToEmission * emissionBoost, velocity01);
        float maxIntensity = Mathf.Clamp(targetIntensity, minimumEmissionIntensity, maxEmissionIntensity);

        // Extra logging for toms
        if (zoneRef.midiNote == 48 || zoneRef.midiNote == 45 || zoneRef.midiNote == 43)
        {
            Debug.Log($"[MidiHit] TOM EMISSION: MIDI {zoneRef.midiNote}, intensity={maxIntensity:F2}, velocity={velocity01:F2}, color={zoneRef.emissionColor}");
        }

        // SET EMISSION TO FULL BRIGHTNESS
        foreach (var mat in mats)
        {
            if (mat == null)
            {
                Debug.LogWarning($"[MidiHit] Null material in {zoneRef.hitZone?.name}!");
                continue;
            }

            mat.EnableKeyword("_EMISSION");
            // ensure realtime emissive flag
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // Use full configured color (linear) and scale by HDR intensity
            Color emission = zoneRef.emissionColor.linear * maxIntensity;
            
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emission);
                
                // Extra logging for toms
                if (zoneRef.midiNote == 48 || zoneRef.midiNote == 45 || zoneRef.midiNote == 43)
                {
                    Debug.Log($"[MidiHit] Set emission on material '{mat.name}': {emission}, hasEmissionProperty={mat.HasProperty("_EmissionColor")}, shader={mat.shader.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[MidiHit] Material '{mat.name}' on {zoneRef.hitZone?.name} doesn't have _EmissionColor property! Shader: {mat.shader.name}");
            }
        }

        // HOLD AT FULL BRIGHTNESS
        yield return new WaitForSeconds(hitVisualDuration);

        // FADE OUT
        float t = 0f;
        float fadeDur = hitVisualDuration;

        while (t < fadeDur)
        {
            t += Time.deltaTime;
            float f = 1f - (t / fadeDur);

            foreach (var mat in mats)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    Color emission = zoneRef.emissionColor.linear * (maxIntensity * f);
                    mat.SetColor("_EmissionColor", emission);
                }
            }
            yield return null;
        }

        // FORCE RESET TO BLACK - CRITICAL FOR CLEANUP
        foreach (var mat in mats)
        {
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", Color.black);
            }
        }

        zoneRef.activePulse = null;
    }
    
    // Safety cleanup method - call this if you suspect stuck emission
    public void ForceResetAllEmission()
    {
        foreach (var mapping in mappings)
        {
            if (mapping.activePulse != null)
            {
                StopCoroutine(mapping.activePulse);
                mapping.activePulse = null;
            }
            
            foreach (var mat in mapping.runtimeMaterials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }
        
        Debug.Log("[MidiHit] Force reset all emission to black");
    }
}
