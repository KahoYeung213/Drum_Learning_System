using UnityEngine;

public class FallingNote : MonoBehaviour
{
    private NoteData noteData;
    private Transform target;
    private float speed;
    private float lifetime;
    private float spawnTime;
    private bool hasHit = false;

    public void Initialize(NoteData data, Transform hitTarget, float fallSpeed, float noteLifetime)
    {
        noteData = data;
        target = hitTarget;
        speed = fallSpeed;
        lifetime = noteLifetime;
        spawnTime = Time.time;
        
        // Set note color based on velocity (optional visual feedback)
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            float normalizedVelocity = Mathf.Clamp01(data.velocity / 127f);
            renderer.material.color = Color.Lerp(Color.gray, Color.red, normalizedVelocity);
        }
    }

    void Update()
    {
        if (hasHit) return;

        // Move towards target
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Check if reached target
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance < 0.5f) // Hit threshold
        {
            HitTarget();
        }

        // Auto-destroy after lifetime
        if (Time.time - spawnTime > lifetime)
        {
            Destroy(gameObject);
        }
    }

    void HitTarget()
    {
        hasHit = true;
        
        // Find MidiHit component and trigger the hit
        MidiHit midiHit = FindObjectOfType<MidiHit>();
        if (midiHit != null)
        {
            // Convert lane to MIDI note (you may need to adjust this mapping)
            int midiNote = GetMidiNoteFromLane(noteData.lane);
            float velocity = noteData.velocity / 127f; // Normalize velocity
            
            // Call the public TriggerHit method directly
            midiHit.TriggerHit(midiNote, velocity);
        }

        // Destroy the note
        Destroy(gameObject, 0.1f);
    }

    int GetMidiNoteFromLane(int lane)
    {
        // Map lane numbers to MIDI notes
        // Adjust these values to match your drum kit MIDI mapping
        int[] midiMapping = { 36, 38, 42, 46, 49, 51 }; // Example: Kick, Snare, Hi-hat, etc.
        
        if (lane >= 0 && lane < midiMapping.Length)
            return midiMapping[lane];
        
        return 36; // Default to kick drum
    }
}