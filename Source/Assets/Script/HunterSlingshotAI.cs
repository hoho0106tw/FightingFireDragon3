using UnityEngine;

[DisallowMultipleComponent]
public sealed class HunterSlingshotAI : MonoBehaviour
{
    [SerializeField] private bool carriesSlingshot = true;
    [SerializeField, Min(.1f)] private float aimSeconds = .8f;
    [SerializeField, Min(.1f)] private float cooldown = 1.25f;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform supportArm;
    [SerializeField] private Transform drawArm;
    [SerializeField] private Transform supportHand;
    [SerializeField] private Transform drawHand;
    [SerializeField] private Transform forkLeft;
    [SerializeField] private Transform forkRight;
    [SerializeField] private LineRenderer band;
    private static readonly int AimParameter = Animator.StringToHash("IsAiming");
    private Quaternion supportRest;
    private Quaternion drawRest;
    private float aimTime;
    private float drawProgress;
    private float readyAt;
    private bool animatorHasAim;
    private bool usesBow;
    private bool throwsStone;
    private Transform releasePoint;
    private Transform bow;
    private Transform nockedArrow;
    private Vector3 aimPoint;
    private ForestAnimeHunter cutoutLook;

    public bool CanShoot => carriesSlingshot && (releasePoint != null || forkLeft != null && forkRight != null);
    public bool IsAiming { get; private set; }
    public bool IsReadyToAim => CanShoot && Time.time >= readyAt;

    public void ConfigureRole(bool armed, bool usesBow, bool throwByHand, float delay)
    {
        carriesSlingshot = armed;
        this.usesBow = usesBow;
        throwsStone = throwByHand;
        cooldown = delay;
    }

    public void BindRig(Transform proceduralSupportArm, Transform proceduralDrawArm, Animator visualAnimator,
        Material material, Transform firePoint, bool showBowVisual = true)
    {
        if (!carriesSlingshot)
            return;
        if (usesBow && !showBowVisual)
            TryGetComponent(out cutoutLook);
        releasePoint = firePoint;
        if (animator == null)
            animator = visualAnimator;
        if (animator != null && animator.avatar != null && animator.isHuman)
        {
            if (supportArm == null)
                supportArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (drawArm == null)
                drawArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (supportHand == null)
                supportHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (drawHand == null)
                drawHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        if (supportArm == null)
            supportArm = proceduralSupportArm;
        if (drawArm == null)
            drawArm = proceduralDrawArm;
        if (drawArm == null)
            return;
        drawRest = drawArm.localRotation;
        if (drawHand == null)
            drawHand = Socket(drawArm, throwsStone ? "Throw release" : "Pulling fingers", new Vector3(.219f, -.526f, .351f));
        if (throwsStone)
        {
            if (releasePoint == null)
                releasePoint = drawHand;
            return;
        }
        // 簡化骨架可只指定 Fire Point，完整骨架則會播放拉弓或拉彈弓的動作。
        if (supportArm == null)
            return;
        supportRest = supportArm.localRotation;
        if (supportHand == null)
        {
            supportHand = Socket(supportArm, "Weapon grip", new Vector3(-.113f, -.591f, .12f));
            supportHand.localRotation = Quaternion.Euler(56f, 0f, -15f);
        }
        if (animator != null)
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                animatorHasAim |= parameter.nameHash == AimParameter && parameter.type == AnimatorControllerParameterType.Bool;

        if (usesBow)
        {
            bow = Socket(supportHand, "Longbow", Vector3.zero);
            LineRenderer limbs = Line(bow, "Curved wooden limbs", material, .047f, false, new Color(.32f, .19f, .10f));
            limbs.positionCount = 13;
            for (int i = 0; i < limbs.positionCount; i++)
            {
                float height = i / 6f - 1f;
                limbs.SetPosition(i, new Vector3(0f, .61f * height, -.27f * height * height));
            }
            LineRenderer grip = Line(bow, "Leather bow grip", material, .068f, false, new Color(.17f, .105f, .07f));
            grip.positionCount = 2;
            grip.SetPositions(new[] { Vector3.down * .105f, Vector3.up * .105f });
            forkLeft = Socket(bow, "Lower bow tip", new Vector3(0f, -.61f, -.27f));
            forkRight = Socket(bow, "Upper bow tip", new Vector3(0f, .61f, -.27f));
            band = Line(bow, "Bowstring", material, .014f, true, new Color(.79f, .70f, .52f));
            nockedArrow = Socket(bow, "Nocked arrow", Vector3.zero);
            if (showBowVisual)
                ForestStone.AddArrowVisual(nockedArrow, material);
            limbs.enabled = grip.enabled = band.enabled = showBowVisual;
            aimSeconds = Mathf.Max(aimSeconds, .95f);
        }
        else if (forkLeft == null || forkRight == null)
        {
            // 遊戲用的 Y 型道具，視覺與石子飛行分離。
            forkLeft = Socket(supportHand, "Left fork", new Vector3(-.13f, .31f, 0f));
            forkRight = Socket(supportHand, "Right fork", new Vector3(.13f, .31f, 0f));
            LineRenderer fork = Line(supportHand, "Wooden slingshot", material, .065f, false, new Color(.51f, .31f, .17f));
            fork.positionCount = 5;
            fork.SetPositions(new[] { forkLeft.localPosition, Vector3.up * .09f, Vector3.zero,
                Vector3.up * .09f, forkRight.localPosition });
        }
        if (band == null)
            band = Line(supportHand, "Draw band", material, .026f, true, new Color(.43f, .25f, .14f));
        band.positionCount = 3;
        UpdateBand(0f);
    }

    public void AdvanceAim(ForestDinosaur dinosaur, Material projectileMaterial, Projectile prefab, float speed)
    {
        if (!IsReadyToAim)
            return;
        aimPoint = dinosaur.ProjectileTarget;
        if (!IsAiming)
        {
            aimTime = 0f;
            SetAiming(true);
        }
        aimTime += Time.deltaTime;
        drawProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(aimTime / aimSeconds));
        cutoutLook?.ShowAttackWindup(drawProgress, aimPoint);
        ApplyDrawPose();
        UpdateBand(drawProgress);
        if (aimTime < aimSeconds)
            return;
        Vector3 launch;
        if (releasePoint != null)
        {
            launch = releasePoint.position;
        }
        else if (usesBow)
        {
            float clearance = Vector3.Distance(nockedArrow.position, aimPoint);
            float offset = Mathf.Min(.96f * nockedArrow.lossyScale.z, clearance * .45f);
            launch = nockedArrow.position + nockedArrow.forward * offset;
        }
        else
        {
            launch = (forkLeft.position + forkRight.position) * .5f;
        }
        // 對準放手當下的恐龍位置。恐龍之後移動，就有機會閃開。
        if (cutoutLook != null && cutoutLook.TryGetAttackOrigin(out Vector3 visualOrigin))
            launch = visualOrigin;
        dinosaur.LaunchProjectile(prefab, transform, launch, projectileMaterial, speed,
            usesBow ? ProjectileKind.Arrow : ProjectileKind.Stone);
        readyAt = Time.time + cooldown;
        FinishAim(launch);
    }

    private void LateUpdate()
    {
        if (IsAiming)
            ApplyDrawPose();
        UpdateBand(drawProgress);
    }

    private void ApplyDrawPose()
    {
        if (animatorHasAim || drawArm == null)
            return;
        float draw = drawProgress;
        if (throwsStone)
        {
            // 先把持石的手拉到肩後，再向前揮到放手的位置。
            float windup = Mathf.SmoothStep(0f, 1f, draw / .68f);
            float release = Mathf.SmoothStep(0f, 1f, (draw - .68f) / .32f);
            drawArm.localRotation = drawRest * Quaternion.Euler(52f * windup - 126f * release, 22f * windup, -14f * windup);
            return;
        }
        if (supportArm == null)
            return;
        supportArm.localRotation = supportRest * Quaternion.Euler((usesBow ? -79f : -64f) * draw, 5f * draw, 17f * draw);
        drawArm.localRotation = drawRest * Quaternion.Euler((usesBow ? -63f : -48f) * draw, -69f * draw, -27f * draw);
    }

    public void CancelAim() => FinishAim(null);

    private void FinishAim(Vector3? launchPosition)
    {
        if (!IsAiming)
            return;
        SetAiming(false);
        cutoutLook?.RecoverAttack(launchPosition);
        drawProgress = 0f;
        if (!animatorHasAim && drawArm != null)
        {
            if (!throwsStone && supportArm != null && supportHand != null)
                supportArm.localRotation = supportRest;
            drawArm.localRotation = drawRest;
        }
        UpdateBand(0f);
    }

    private void SetAiming(bool value)
    {
        IsAiming = value;
        if (animatorHasAim)
            animator.SetBool(AimParameter, value);
    }

    private void UpdateBand(float draw)
    {
        if (band == null)
            return;
        if (bow != null)
        {
            bow.localRotation = Quaternion.Euler(0f, 0f, -21f);
            Vector3 direction = aimPoint - bow.position;
            if (draw > 0f && direction.sqrMagnitude > .0001f)
                bow.rotation = Quaternion.Slerp(bow.rotation, Quaternion.LookRotation(direction), draw);
        }
        Vector3 a = forkLeft.position;
        Vector3 b = forkRight.position;
        Vector3 middle = Vector3.Lerp((a + b) * .5f, drawHand.position, draw);
        if (bow != null)
        {
            Vector3 drawPosition = bow.InverseTransformPoint(drawHand.position);
            drawPosition.z = Mathf.Clamp(drawPosition.z, -.88f, -.36f);
            middle = bow.TransformPoint(Vector3.Lerp(new Vector3(0f, 0f, -.27f), drawPosition, draw));
            nockedArrow.gameObject.SetActive(draw > 0f);
            nockedArrow.position = middle;
            nockedArrow.rotation = draw > 0f && (aimPoint - middle).sqrMagnitude > .0001f
                ? Quaternion.LookRotation(aimPoint - middle)
                : bow.rotation;
        }
        band.SetPosition(0, a);
        band.SetPosition(1, middle);
        band.SetPosition(2, b);
    }

    private static Transform Socket(Transform parent, string label, Vector3 position)
    {
        var socket = new GameObject(label).transform;
        socket.SetParent(parent, false);
        socket.localPosition = position;
        return socket;
    }

    private static LineRenderer Line(Transform parent, string label, Material material, float width, bool world, Color tint)
    {
        LineRenderer line = Socket(parent, label, Vector3.zero).gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", tint);
        line.SetPropertyBlock(properties);
        line.generateLightingData = true;
        line.widthMultiplier = width;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.useWorldSpace = world;
        return line;
    }
}
