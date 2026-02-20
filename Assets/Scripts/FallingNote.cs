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

    public void Initialize(Vector3 target, float duration, GameObject drumMesh, float hitTime)
    {
        _startPos = transform.position;
        _endPos = target;
        _fallDuration = duration;
        _drumMesh = drumMesh;
        _hitTime = hitTime;
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
        var originalColor = renderer.material.color;
        renderer.material.color = Color.yellow;

        yield return new WaitForSeconds(0.15f);

        renderer.material.color = originalColor;
        Destroy(gameObject);
    }
}