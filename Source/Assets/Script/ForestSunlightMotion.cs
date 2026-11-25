using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class ForestSunlightMotion : MonoBehaviour
{
    [SerializeField] private Transform shafts;
    [SerializeField, Min(4f)] private float cycleSeconds = 24f;
    [SerializeField, Range(0f, 20f)] private float yawDegrees = 7f;
    [SerializeField, Range(0f, 10f)] private float pitchDegrees = 3.5f;
    [SerializeField, Range(0f, .4f)] private float intensityVariation = .18f;

    private static readonly int LightResponse = Shader.PropertyToID("_LightResponse");

    private Light sun;
    private Quaternion restingRotation;
    private float restingIntensity;
    private float startedAt;
    private MeshRenderer[] beams;
    private MaterialPropertyBlock properties;

    private void OnEnable()
    {
        sun = GetComponent<Light>();
        restingRotation = transform.localRotation;
        restingIntensity = sun.intensity;
        startedAt = Time.time;
        beams = shafts != null ? shafts.GetComponentsInChildren<MeshRenderer>(true) : System.Array.Empty<MeshRenderer>();
        properties ??= new MaterialPropertyBlock();
    }

    private void Update()
    {
        float phase = (Time.time - startedAt) * (2f * Mathf.PI / cycleSeconds);
        float sweep = Mathf.Sin(phase);
        float gust = Mathf.Sin(phase * 2f);
        var yaw = Quaternion.AngleAxis(yawDegrees * sweep, Vector3.up);
        var pitch = Quaternion.AngleAxis(pitchDegrees * gust, Vector3.right);
        transform.localRotation = yaw * restingRotation * pitch;

        float strength = 1f + intensityVariation * (.72f * sweep + .28f * gust);
        sun.intensity = restingIntensity * strength;
        for (int index = 0; index < beams.Length; index++)
        {
            float offset = index * 1.1f;
            float drift = Mathf.Sin(phase * 2f + offset) - Mathf.Sin(offset);
            SetBeamResponse(beams[index], strength * (1f + .08f * drift));
        }
    }

    private void OnDisable()
    {
        if (sun == null)
            return;
        transform.localRotation = restingRotation;
        sun.intensity = restingIntensity;
        foreach (var beam in beams)
            SetBeamResponse(beam, 1f);
    }

    private void SetBeamResponse(MeshRenderer beam, float response)
    {
        if (beam == null)
            return;
        beam.GetPropertyBlock(properties);
        properties.SetFloat(LightResponse, response);
        beam.SetPropertyBlock(properties);
    }
}
