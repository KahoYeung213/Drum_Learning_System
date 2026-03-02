using UnityEngine;

public class FallingNote : MonoBehaviour
{
    private Vector3 _startPos;
    private Vector3 _endPos;
    private float _fallDuration;
    private float _elapsed;
    private GameObject _drumMesh;
    private bool _hasArrived;
    private float _hitTime;
    private int _lane;
    private Color _emissionColor = Color.white;
    private bool _isHit = false; // Track if this note has been hit
    
    // Public properties for accessing data (used for hit detection and seeking)
    public float HitTime => _hitTime;
    public int Lane => _lane;
    public bool IsHit => _isHit;
    
    public void MarkAsHit()
    {
        _isHit = true;
    }

    public void Initialize(Vector3 target, float duration, GameObject drumMesh, float hitTime, Color emissionColor, int lane)
    {
        _startPos = transform.position;
        _endPos = target;
        _fallDuration = duration;
        _drumMesh = drumMesh;
        _hitTime = hitTime;
        _lane = lane;
        _emissionColor = emissionColor;
        _elapsed = 0f;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _fallDuration);

        // Move smoothly from spawn point to drum piece
        transform.position = Vector3.Lerp(_startPos, _endPos, t);

        if (t >= 1f && !_hasArrived)
        {
            _hasArrived = true;
            OnArrived();
        }
    }

    void OnArrived()
    {
        // Light up the drum mesh
        StartCoroutine(LightUpDrum());
    }

    System.Collections.IEnumerator LightUpDrum()
    {
        var renderer = _drumMesh.GetComponent<Renderer>();
        Material mat = renderer.material;
        
        // Store original emission
        Color originalEmission = Color.black;
        if (mat.HasProperty("_EmissionColor"))
        {
            originalEmission = mat.GetColor("_EmissionColor");
        }
        
        // Enable emission and set to the hitzone's emission color
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        
        if (mat.HasProperty("_EmissionColor"))
        {
            // Use HDR emission for visibility (intensity boost)
            mat.SetColor("_EmissionColor", _emissionColor.linear * 8.0f);
        }

        yield return new WaitForSeconds(0.15f);

        // Restore original emission
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", originalEmission);
        }
        
        Destroy(gameObject);
    }
}