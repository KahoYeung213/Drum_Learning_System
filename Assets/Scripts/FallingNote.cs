using UnityEngine;
using System.Collections.Generic;

public class FallingNote : MonoBehaviour
{
    private static readonly Dictionary<Renderer, FlashState> FlashStates = new Dictionary<Renderer, FlashState>();

    private class FlashState
    {
        public int token;
        public Color baselineEmission = Color.black;
        public bool hasBaseline;
    }

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

    [Header("Flash Safety")]
    [SerializeField, Range(0f, 3f)] private float emissionIntensity = 0.35f;
    [SerializeField, Range(0.02f, 0.5f)] private float flashDuration = 0.08f;
    
    [Header("Note Visual")]
    [SerializeField, Range(0f, 3f)] private float noteEmissionIntensity = 0.25f;
    
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

        ApplyLaneColorToNote();
    }

    void ApplyLaneColorToNote()
    {
        var noteRenderer = GetComponent<Renderer>();
        if (noteRenderer == null) return;

        Material noteMat = noteRenderer.material;

        if (noteMat.HasProperty("_Color"))
        {
            noteMat.SetColor("_Color", _emissionColor);
        }

        if (noteMat.HasProperty("_BaseColor"))
        {
            noteMat.SetColor("_BaseColor", _emissionColor);
        }

        if (noteMat.HasProperty("_EmissionColor"))
        {
            noteMat.EnableKeyword("_EMISSION");
            noteMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            noteMat.SetColor("_EmissionColor", _emissionColor.linear * noteEmissionIntensity);
        }
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
        if (renderer == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Material mat = renderer.material;
        if (!FlashStates.TryGetValue(renderer, out var flashState))
        {
            flashState = new FlashState();
            FlashStates[renderer] = flashState;
        }

        if (!flashState.hasBaseline && mat.HasProperty("_EmissionColor"))
        {
            flashState.baselineEmission = mat.GetColor("_EmissionColor");
            flashState.hasBaseline = true;
        }

        int myToken = ++flashState.token;
        
        // Enable emission and set to the hitzone's emission color
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", _emissionColor.linear * emissionIntensity);
        }

        yield return new WaitForSeconds(flashDuration);

        if (!FlashStates.TryGetValue(renderer, out flashState) || flashState.token != myToken)
        {
            Destroy(gameObject);
            yield break;
        }

        // Restore the emission value that was present before the first overlapping flash
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", flashState.baselineEmission);
        }

        flashState.hasBaseline = false;
        
        Destroy(gameObject);
    }
}