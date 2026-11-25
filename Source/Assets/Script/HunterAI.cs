using UnityEngine;

internal enum ForestMageLook { Forest, Goblin, WangHao }

public class HunterAI : MonoBehaviour
{
    private enum WeaponType { SceneDefault, Melee, Stone, Bow }
    private const float MeleeWindup = .27f;
    private const float MeleeDuration = .59f;
    private const float CastWindup = .72f;
    private const float CastCooldown = 1.8f;
    private const int HealThreshold = 80;
    private const int HealPerTick = 5;
    private const float HealInterval = .75f;

    [SerializeField] private ForestDinosaur dinosaur;
    [SerializeField] private Transform model;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;
    [SerializeField] private Transform weaponArm;
    [SerializeField] private Transform supportArm;
    [SerializeField] private GameObject warning;
    [SerializeField] private ParticleSystem embers;

    [SerializeField] private bool usesSlingshot;
    [Header("武器與攻擊")]
    [SerializeField] private WeaponType weapon = WeaponType.Stone;
    [SerializeField] private Projectile stonePrefab;
    [SerializeField] private Projectile arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(.5f)] private float attackRange = 12f;
    [SerializeField, Min(.1f)] private float attackCooldown = 1.25f;
    [SerializeField, Min(1f)] private float stoneHorizontalSpeed = 13f;
    [SerializeField, Min(1f)] private float arrowSpeed = 23f;
    [Header("玩家偵測")]
    [SerializeField, Min(.5f)] private float detectRadius = 15f;
    [SerializeField, Min(0f)] private float patrolRadius = .78f;
    [Tooltip("可遮住獵人視線、也會擋住獵人移動的 Layer。")]
    [SerializeField] private LayerMask obstacleLayers = Physics.DefaultRaycastLayers;

    private Vector3 post;
    private float patrolPhase;
    private float walkWeight;
    private float fallStartedAt;
    private Material stoneMaterial;
    private bool isMage;
    private ForestMageLook mageLook;
    private HunterAI healingTarget;
    private HunterAI healer;
    private float nextHealTick;
    private AudioSource chant;
    private Transform staffTip;
    private Vector3 staffTipScale;
    private float castEndsAt;
    private float castReadyAt;
    private HunterSlingshotAI slingshot;
    private BreathingEffect breathing;
    private ForestAnimeHunter look;
    private Quaternion modelRest;
    private Quaternion weaponRest;
    private Quaternion supportRest;
    private float meleeStartedAt = float.NegativeInfinity;
    private float meleeReadyAt;
    private bool meleeReleased;
    private float pursuitUntil;
    private bool usesBow;

    public bool IsAlive { get; private set; } = true;
    public bool IsBoss => mageLook == ForestMageLook.WangHao;
    public bool IsReceivingHealing => healer != null && healer.IsHealing;
    public bool IsHealing { get; private set; }
    public int MaxHealth { get; private set; } = 30;
    public int Health { get; private set; } = 30;
    public Transform ImpactBody => model;

    private void Awake()
    {
        post = transform.position;
        patrolPhase = post.x * 0.7f + post.z;
        Material source = model.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        stoneMaterial = new Material(source);
        stoneMaterial.SetColor("_BaseColor", new Color(0.57f, 0.61f, 0.66f));
        stoneMaterial.SetFloat("_Smoothness", 0.22f);
    }

    private void Start()
    {
        // 場景可以直接指定玩家，也可以交給 Player Tag 尋找。
        if (dinosaur == null)
            dinosaur = GameObject.FindWithTag("Player")?.GetComponent<ForestDinosaur>();
        TryGetComponent(out look);
        if (look == null || !look.SetGameplayLook(isMage, mageLook))
            ForestHunterDetail.Apply(model, weaponArm);
        if (look != null && look.SupportArm != null)
            supportArm = look.SupportArm;
        modelRest = model.localRotation;
        weaponRest = weaponArm.localRotation;
        if (supportArm != null)
            supportRest = supportArm.localRotation;
        if (!TryGetComponent(out breathing))
            breathing = gameObject.AddComponent<BreathingEffect>();
        breathing.Configure(look != null && look.Chest != null ? look.Chest : model, supportArm, weaponArm);
        if (isMage)
        {
            staffTip = look != null ? look.StaffTip : null;
            if (staffTip != null)
                staffTipScale = staffTip.localScale;
            stoneMaterial.SetColor("_BaseColor", new Color(.23f, .64f, 1f));
            stoneMaterial.EnableKeyword("_EMISSION");
            stoneMaterial.SetColor("_EmissionColor", new Color(.32f, .83f, 1.65f));
            if (healingTarget != null)
                ConfigureChant();
            return;
        }
        usesBow = weapon == WeaponType.Bow;
        bool ranged = usesBow || weapon == WeaponType.Stone || (weapon == WeaponType.SceneDefault && usesSlingshot);
        if (!TryGetComponent(out slingshot))
            slingshot = gameObject.AddComponent<HunterSlingshotAI>();
        slingshot.ConfigureRole(ranged, usesBow, weapon == WeaponType.Stone && !usesBow, attackCooldown);
        slingshot.BindRig(supportArm, weaponArm, look != null ? look.VisualAnimator : null, stoneMaterial, firePoint,
            showBowVisual: look == null || !look.UsesCharacterCutout);
    }

    internal void MakeMage(ForestMageLook appearance = ForestMageLook.Forest)
    {
        mageLook = appearance;
        gameObject.name = appearance switch
        {
            ForestMageLook.WangHao => "魔王",
            ForestMageLook.Goblin => "Goblin mage",
            _ => "Forest mage"
        };
        isMage = true;
        MaxHealth = Health = appearance == ForestMageLook.Goblin ? 60 : 100;
    }

    internal void SupportBoss(HunterAI boss)
    {
        healingTarget = boss;
        boss.healer = this;
        gameObject.name = "Goblin support mage";
        MaxHealth = Health = 100;
    }

    internal void MakeOpeningElf(Vector3 position)
    {
        gameObject.name = "Opening forest elf";
        transform.position = post = position;
        patrolPhase = post.x * .7f + post.z;
        patrolRadius = .35f;
        detectRadius = 3.7f;
        weapon = WeaponType.Melee;
        MaxHealth = Health = 10;
    }

    private void Update()
    {
        float now = Time.time;
        if (!IsAlive)
        {
            breathing.Sample(0f, resetImmediately: true);
            float fall = Mathf.Clamp01((now - fallStartedAt) / 0.42f);
            model.localRotation = modelRest * Quaternion.Euler(-84f * fall, 0f, 0f);
            if (now - fallStartedAt > 1.5f)
                gameObject.SetActive(false);
            return;
        }
        if (dinosaur != null && dinosaur.IsKnockedOut)
        {
            AnimateWalk(false);
            return;
        }

        if (IsHealing)
        {
            if (healingTarget != null && healingTarget.IsAlive)
                Face(healingTarget.transform.position - transform.position);
            AnimateWalk(false, true);
            AdvanceHealing(now);
            return;
        }

        if (dinosaur != null)
        {
            // 偵測只量地面距離，視線則從眼睛射到恐龍身體中心。
            Vector3 playerOffset = dinosaur.transform.position - transform.position;
            playerOffset.y = 0f;
            float distance = playerOffset.magnitude;
            bool inSight = distance <= detectRadius && HasLineOfSight();
            if (inSight)
                pursuitUntil = now + 4f;
            if (now < pursuitUntil && distance <= detectRadius)
            {
                Engage(playerOffset, distance, inSight, now);
                return;
            }
        }

        // 沒有目標時，定期換一個巡邏點。
        StopAttack();
        float heading = Mathf.Floor((now + Mathf.Repeat(patrolPhase, 3.8f)) / 3.8f) * 2.1f + patrolPhase;
        Vector3 patrol = post + new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading)) * patrolRadius;
        Vector3 patrolOffset = patrol - transform.position;
        Face(patrolOffset);
        AnimateWalk(MoveOnGround(patrolOffset.normalized, Mathf.Min(patrolOffset.magnitude, Time.deltaTime * .7f)));
    }

    private bool HasLineOfSight()
    {
        Vector3 eye = transform.position + Vector3.up * (1.95f * transform.lossyScale.y);
        // 忽略警戒區等 Trigger。第一個實體 Collider 若是樹幹或障礙物，就暫停攻擊。
        return !Physics.Linecast(eye, dinosaur.ProjectileTarget, out RaycastHit hit, obstacleLayers,
            QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<ForestDinosaur>() == dinosaur;
    }

    private void Engage(Vector3 offset, float distance, bool inSight, float now)
    {
        Face(offset);
        const float reach = 1.8f;
        if (isMage && inSight && distance <= attackRange)
        {
            AnimateWalk(false, castEndsAt > 0f || now >= castReadyAt);
            AdvanceCast(now);
            return;
        }
        if (now - meleeStartedAt >= MeleeDuration)
            CancelMelee();
        // 恐龍已經貼身時仍會出手，避免為了拉開射程而一直往後退。
        if (!isMage && !usesBow && (now - meleeStartedAt < MeleeDuration ||
            (inSight && distance <= reach && now >= meleeReadyAt)))
        {
            slingshot.CancelAim();
            AnimateWalk(false, true);
            AdvanceMelee(distance, reach, inSight, now);
            return;
        }
        if (slingshot != null && slingshot.CanShoot && inSight && distance <= attackRange &&
            (usesBow || distance > reach + .32f))
        {
            AnimateWalk(false, slingshot.IsAiming || slingshot.IsReadyToAim);
            slingshot.AdvanceAim(dinosaur, stoneMaterial, usesBow ? arrowPrefab : stonePrefab,
                usesBow ? arrowSpeed : stoneHorizontalSpeed);
            warning?.SetActive(slingshot.IsAiming);
            return;
        }

        StopAttack();
        // 偵測到了但還沒進入射程時，往前追到能出手的位置。
        float approach = Mathf.Max(0f, distance - reach * .68f);
        float speed = isMage || (slingshot != null && slingshot.CanShoot) ? 2.7f : 3.25f;
        float advance = Mathf.Min(approach, speed * Time.deltaTime);
        AnimateWalk(MoveOnGround(offset.normalized, advance), running: true);
    }

    private void AdvanceCast(float now)
    {
        if (now < castReadyAt)
            return;
        if (castEndsAt == 0f)
        {
            castEndsAt = now + CastWindup;
            warning?.SetActive(true);
        }

        float charge = Mathf.SmoothStep(0f, 1f, 1f - (castEndsAt - now) / CastWindup);
        look?.ShowAttackWindup(charge, dinosaur.ProjectileTarget);
        weaponArm.localRotation = weaponRest * Quaternion.Euler(27f * charge, -15f * charge, -9f * charge);
        if (staffTip != null)
            staffTip.localScale = staffTipScale * (1f + charge * .85f);
        if (now < castEndsAt)
            return;

        Vector3 origin = staffTip != null ? staffTip.position : transform.TransformPoint(.42f, 1.65f, .45f);
        if (look != null && look.TryGetAttackOrigin(out Vector3 visualOrigin))
            origin = visualOrigin;
        dinosaur.LaunchProjectile(null, transform, origin, stoneMaterial, 14f,
            mageLook == ForestMageLook.Forest ? ProjectileKind.SpiritFire : ProjectileKind.BlueFire);
        castReadyAt = now + CastCooldown;
        CancelCast(origin);
    }

    private void CancelCast(Vector3? launchPosition = null)
    {
        if (castEndsAt == 0f)
            return;
        castEndsAt = 0f;
        look?.RecoverAttack(launchPosition);
        weaponArm.localRotation = weaponRest;
        if (staffTip != null)
            staffTip.localScale = staffTipScale;
        warning?.SetActive(false);
    }

    private bool MoveOnGround(Vector3 direction, float distance)
    {
        if (distance <= .001f)
            return false;
        if (dinosaur != null)
        {
            Vector3 step = dinosaur.SpaceHunterStep(this, direction * distance);
            distance = step.magnitude;
            if (distance <= .001f)
                return false;
            direction = step / distance;
        }
        Vector3 center = transform.position + Vector3.up * .5f;
        if (Physics.SphereCast(center, .24f, direction, out RaycastHit hit, distance + .06f, obstacleLayers,
                QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - .06f);
        transform.position += direction * distance;
        return distance > .001f;
    }

    private void AdvanceMelee(float distance, float reach, bool inSight, float now)
    {
        if (float.IsNegativeInfinity(meleeStartedAt))
        {
            meleeStartedAt = now;
            meleeReadyAt = now + 1.04f;
            meleeReleased = false;
        }
        float age = now - meleeStartedAt;
        float windup = Mathf.SmoothStep(0f, 1f, age / MeleeWindup);
        float strike = Mathf.SmoothStep(0f, 1f, (age - MeleeWindup) / .10f);
        float recovery = 1f - Mathf.SmoothStep(0f, 1f, (age - .37f) / .22f);
        weaponArm.localRotation = weaponRest * Quaternion.Euler(
            (41f * windup - 122f * strike) * recovery, (-22f * windup + 36f * strike) * recovery, 16f * recovery);
        if (supportArm != null)
            supportArm.localRotation = supportRest * Quaternion.Euler(-22f * windup * recovery, 0f, -12f * recovery);
        model.localRotation = modelRest * Quaternion.Euler(9f * strike * recovery, (-18f * windup + 31f * strike) * recovery, 0f);
        warning?.SetActive(age < MeleeWindup);
        if (!meleeReleased && age >= MeleeWindup + .045f)
        {
            meleeReleased = true;
            if (inSight && distance <= reach + .12f)
                dinosaur.TakeMeleeHit(transform.position, 6);
        }
    }

    private void CancelMelee()
    {
        if (float.IsNegativeInfinity(meleeStartedAt))
            return;
        meleeStartedAt = float.NegativeInfinity;
        model.localRotation = modelRest;
        weaponArm.localRotation = weaponRest;
        if (supportArm != null)
            supportArm.localRotation = supportRest;
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 640f);
        }
    }

    private void AnimateWalk(bool moving, bool attacking = false, bool running = false)
    {
        walkWeight = Mathf.MoveTowards(walkWeight, moving ? 1f : 0f, Time.deltaTime * 5f);
        float stride = Mathf.Sin(Time.time * 13f + patrolPhase) * walkWeight;
        float legSwing = stride * (running ? 26f : 12f);
        leftLeg.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);
        breathing.Sample(attacking ? .72f : 1f, walkWeight * (running ? 1f : .65f), stride * 13f, poseArms: !attacking);
    }

    public bool TakeFireDamage(int damage)
    {
        if (!TakeDamage(damage, Time.time))
            return false;
        var ash = new MaterialPropertyBlock();
        ash.SetColor("_BaseColor", new Color(0.19f, 0.12f, 0.09f));
        foreach (Renderer piece in model.GetComponentsInChildren<Renderer>())
        {
            if (piece is SpriteRenderer sprite)
                sprite.color = new Color(.35f, .25f, .21f);
            else
                piece.SetPropertyBlock(ash);
        }
        embers?.Play(false);
        return true;
    }

    public bool TakeBiteDamage(int damage) => TakeDamage(damage, Time.time);

    private bool TakeDamage(int damage, float now)
    {
        if (!IsAlive)
            return false;
        Health = Mathf.Max(0, Health - damage);
        if (Health > 0)
        {
            if (IsBoss && Health <= HealThreshold && healer != null && healer.IsAlive && !healer.IsHealing)
                healer.BeginHealing(now);
            return false;
        }
        IsAlive = false;
        fallStartedAt = now;
        if (healer != null)
            healer.StopHealing();
        StopAttack();
        return true;
    }

    public void StopAttack()
    {
        StopHealing();
        if (slingshot != null)
            slingshot.CancelAim();
        CancelCast();
        CancelMelee();
        warning?.SetActive(false);
    }

    private void BeginHealing(float now)
    {
        StopAttack();
        IsHealing = true;
        nextHealTick = now + HealInterval;
        look?.BeginHealing(now, HealInterval);
        healingTarget.look?.ReceiveHealing(now);
        if (chant != null)
            chant.Play();
    }

    private void AdvanceHealing(float now)
    {
        if (healingTarget == null || !healingTarget.IsAlive)
        {
            StopHealing();
            return;
        }
        if (now < nextHealTick)
            return;

        healingTarget.Health = Mathf.Min(healingTarget.MaxHealth, healingTarget.Health + HealPerTick);
        nextHealTick = now + HealInterval;
        look?.PulseHealing(now);
        healingTarget.look?.ReceiveHealing(now);
        if (healingTarget.Health == healingTarget.MaxHealth)
        {
            StopHealing();
            castReadyAt = Mathf.Max(castReadyAt, now + .35f);
        }
    }

    private void StopHealing()
    {
        if (!IsHealing)
            return;
        IsHealing = false;
        look?.EndHealing();
        if (chant != null)
            chant.Stop();
    }

    private void ConfigureChant()
    {
        AudioClip clip = Resources.Load<AudioClip>("Firelight/Goblin/GoblinChant");
        if (clip == null)
            return;
        chant = gameObject.AddComponent<AudioSource>();
        chant.clip = clip;
        chant.playOnAwake = false;
        chant.loop = true;
        chant.spatialBlend = 1f;
        chant.rolloffMode = AudioRolloffMode.Linear;
        chant.minDistance = 2f;
        chant.maxDistance = 16f;
        chant.volume = .6f;
    }

    private void OnDestroy()
    {
        if (stoneMaterial != null)
            Destroy(stoneMaterial);
    }
}
