using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ProceduralRockThrow : MonoBehaviour
{
    [Serializable]
    public sealed class LandingEvent : UnityEvent<Vector3> { }

    [Header("骨架與投擲物")]
    [SerializeField] private Transform arm;
    [SerializeField] private Transform upperBody;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private Transform landingPoint;
    [SerializeField] private GameObject rockPrefab;

    [Header("拋物線")]
    [Tooltip("飛到一半時，石頭比起點與落點的連線高多少個世界單位。")]
    [SerializeField, Min(0f)] private float arcHeight = 2f;
    [SerializeField, Min(0.05f)] private float flightDuration = 0.9f;
    [SerializeField] private Vector3 spinPerSecond = new(540f, 360f, 120f);

    [Header("手臂與上半身的局部旋轉")]
    [SerializeField] private Vector3 armWindupEuler = new(58f, 0f, -12f);
    [SerializeField] private Vector3 armReleaseEuler = new(-58f, 0f, 5f);
    [SerializeField] private Vector3 bodyWindupEuler = new(0f, -17f, 0f);
    [SerializeField] private Vector3 bodyReleaseEuler = new(9f, 13f, 0f);
    [SerializeField, Min(0.01f)] private float windupDuration = 0.38f;
    [SerializeField, Min(0.01f)] private float swingDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float recoveryDuration = 0.24f;

    [Header("落地時傳入世界座標")]
    [SerializeField] private LandingEvent onLanded = new();

    private sealed class Flight
    {
        public GameObject Rock;
        public Vector3 Start;
        public Vector3 End;
        public float Height;
        public float Duration;
        public float Age;
    }

    private readonly List<Flight> flights = new();
    private Quaternion armRest;
    private Quaternion bodyRest;
    private float throwAge;
    private bool throwing;
    private bool released;

    public LandingEvent OnLanded => onLanded;

    // 可由 UI Button 的 OnClick、其他腳本，或元件右上角選單呼叫。
    [ContextMenu("投擲石頭（Play 模式）")]
    public void Throw()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || throwing)
            return;
        if (arm == null || throwPoint == null || landingPoint == null || rockPrefab == null)
        {
            Debug.LogWarning("請指定手臂、投擲起點、落點與石頭 Prefab。", this);
            return;
        }
        if (arm == upperBody)
        {
            Debug.LogWarning("手臂和上半身必須是不同的骨架節點。", this);
            return;
        }

        // 以出手前的姿勢為基準，保留模型自己的骨架預設角度。
        armRest = arm.localRotation;
        if (upperBody != null)
            bodyRest = upperBody.localRotation;
        throwAge = 0f;
        throwing = true;
        released = false;
    }

    private void Update()
    {
        float step = Time.deltaTime;
        UpdateFlights(step);
        if (!throwing)
            return;

        float windup = Mathf.Max(0.01f, windupDuration);
        float swing = Mathf.Max(0.01f, swingDuration);
        float recovery = Mathf.Max(0.01f, recoveryDuration);
        float releaseTime = windup + swing;
        throwAge += step;
        if (!released && throwAge >= releaseTime)
        {
            // 就算這一幀跨過出手時間，也先套用出手瞬間的骨架姿勢。
            ApplyPose(releaseTime, windup, swing, recovery);
            ReleaseRock();
            released = true;
        }

        ApplyPose(throwAge, windup, swing, recovery);
        if (throwAge >= releaseTime + recovery)
            throwing = false;
    }

    private void ApplyPose(float age, float windup, float swing, float recovery)
    {
        if (upperBody != null)
            upperBody.localRotation = EvaluatePose(bodyRest, bodyWindupEuler, bodyReleaseEuler, age, windup, swing, recovery);
        arm.localRotation = EvaluatePose(armRest, armWindupEuler, armReleaseEuler, age, windup, swing, recovery);
    }

    private void ReleaseRock()
    {
        // 取出手時的手部與落點世界座標，飛行中不追蹤移動中的目標。
        Vector3 start = throwPoint.position;
        Vector3 end = landingPoint.position;
        var rock = Instantiate(rockPrefab, start, throwPoint.rotation);
        flights.Add(new Flight
        {
            Rock = rock,
            Start = start,
            End = end,
            Height = Mathf.Max(0f, arcHeight),
            Duration = Mathf.Max(0.05f, flightDuration)
        });
    }

    private void UpdateFlights(float step)
    {
        for (int index = flights.Count - 1; index >= 0; index--)
        {
            Flight flight = flights[index];
            if (flight.Rock == null)
            {
                flights.RemoveAt(index);
                continue;
            }
            flight.Age = Mathf.Min(flight.Duration, flight.Age + step);
            float t = flight.Age / flight.Duration;
            flight.Rock.transform.position = SampleArc(flight.Start, flight.End, t, flight.Height);
            flight.Rock.transform.Rotate(spinPerSecond * step, Space.Self);
            if (flight.Age < flight.Duration)
                continue;

            // 先從飛行清單移除，確保同一顆石頭只觸發一次事件。
            flights.RemoveAt(index);
            Destroy(flight.Rock);
            onLanded.Invoke(flight.End);
            if (!isActiveAndEnabled)
                return;
            index = Mathf.Min(index, flights.Count);
        }
    }

    private void OnDisable()
    {
        if (throwing)
        {
            if (arm != null)
                arm.localRotation = armRest;
            if (upperBody != null)
                upperBody.localRotation = bodyRest;
            throwing = false;
        }
        foreach (Flight flight in flights)
            Destroy(flight.Rock);
        flights.Clear();
    }

    // 在起點到終點的直線上疊加二次曲線。t = 0.5 時，額外高度等於 height。
    public static Vector3 SampleArc(Vector3 start, Vector3 end, float t, float height)
    {
        return Vector3.LerpUnclamped(start, end, t) + Vector3.up * (4f * height * t * (1f - t));
    }

    // 蓄力、揮出和回復都在局部空間做四元數插值，避免直接跳到下一個角度。
    public static Quaternion EvaluatePose(Quaternion rest, Vector3 windupEuler, Vector3 releaseEuler,
        float age, float windup, float swing, float recovery)
    {
        Quaternion windupPose = rest * Quaternion.Euler(windupEuler);
        Quaternion releasePose = rest * Quaternion.Euler(releaseEuler);
        if (age < windup)
            return Quaternion.Slerp(rest, windupPose, SmoothProgress(age / windup));
        if (age < windup + swing)
            return Quaternion.Slerp(windupPose, releasePose, SmoothProgress((age - windup) / swing));
        return Quaternion.Slerp(releasePose, rest, SmoothProgress((age - windup - swing) / recovery));
    }

    private static float SmoothProgress(float t)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }
}
