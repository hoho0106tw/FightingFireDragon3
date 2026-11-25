using UnityEngine;

[DisallowMultipleComponent]
public sealed class BreathingEffect : MonoBehaviour
{
    private Transform chest;
    private Transform leftArm;
    private Transform rightArm;
    private Vector3 chestScale;
    private Vector3 chestPosition;
    private Vector3 leftPosition;
    private Vector3 rightPosition;
    private Quaternion leftRotation;
    private Quaternion rightRotation;
    private float phase;
    private float weight;
    private bool armsPosed;

    public float Expansion { get; private set; }

    public void Configure(Transform chest, Transform leftArm, Transform rightArm)
    {
        this.chest = chest;
        this.leftArm = leftArm;
        this.rightArm = rightArm;
        chestScale = chest.localScale;
        chestPosition = chest.localPosition;
        if (leftArm != null)
        {
            leftPosition = leftArm.localPosition;
            leftRotation = leftArm.localRotation;
        }
        if (rightArm != null)
        {
            rightPosition = rightArm.localPosition;
            rightRotation = rightArm.localRotation;
        }
        phase = Mathf.Repeat(GetInstanceID() * .017f, 1f);
    }

    public void Sample(float influence, float movement = 0f, float armSwing = 0f,
        bool resetImmediately = false, bool poseArms = true)
    {
        if (chest == null)
            return;
        phase = Mathf.Repeat(phase + Time.deltaTime * Mathf.Lerp(.274f, .40f, movement), 1f);
        float next = resetImmediately ? 0f : Mathf.MoveTowards(weight, influence, Time.deltaTime * 4f);
        if (weight == 0f && next == 0f)
            return;
        weight = next;

        // The longer exhale and delayed arm motion soften the change of direction.
        float depth = weight * Mathf.Lerp(1f, 1.18f, movement);
        float breath = Expansion = Wave(phase) * depth;
        float shoulder = Wave(Mathf.Repeat(phase - .05f, 1f)) * depth;
        float arm = Wave(Mathf.Repeat(phase - .09f, 1f)) * depth;
        chest.localScale = Vector3.Scale(chestScale, new Vector3(1f + .060f * breath, 1f + .032f * breath, 1f + .105f * breath));
        chest.localPosition = chestPosition + new Vector3(0f, .048f * breath, .022f * breath);
        if (poseArms || armsPosed)
        {
            float lift = poseArms ? shoulder : 0f;
            float sway = poseArms ? arm : 0f;
            float swing = poseArms ? armSwing * weight : 0f;
            PoseArm(leftArm, leftPosition, leftRotation, -1f, lift, sway, swing);
            PoseArm(rightArm, rightPosition, rightRotation, 1f, lift, sway, swing);
        }
        armsPosed = poseArms && weight > 0f;
    }

    private static float Wave(float cycle) => cycle < .42f
        ? Mathf.SmoothStep(0f, 1f, cycle / .42f)
        : Mathf.SmoothStep(1f, 0f, (cycle - .42f) / .58f);

    private static void PoseArm(Transform arm, Vector3 position, Quaternion rotation, float side, float shoulder,
        float breath, float swing)
    {
        if (arm == null)
            return;
        arm.localPosition = position + new Vector3(side * .030f, .057f, .018f) * shoulder;
        arm.localRotation = Quaternion.Euler(side * swing - 4.3f * breath, 0f, side * 5.6f * breath) * rotation;
    }

    private void OnDisable() => Sample(0f, resetImmediately: true);
}
