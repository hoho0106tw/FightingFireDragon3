using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-10)]
public class ForestDinosaur : MonoBehaviour
{
    [SerializeField] private Transform followCamera;
    [SerializeField] private SpriteRenderer character;
    [SerializeField] private Material forestSurface;
    [SerializeField] private HunterAI[] hunters;
    [SerializeField] private Vector2 meadowBounds = new Vector2(11f, 14f);
    [SerializeField] private float moveSpeed = 5.5f;

    private static readonly Vector3 CameraOffset = new Vector3(0f, 8.2f, -12f);
    private const float OpeningSpawnDepth = -12f;
    private const float ForestBattleDepth = -5.5f;
    private const float FireReach = 7.2f;
    private const float TouchFireDistance = FireReach - 1.2f;
    private const float FireTurnDuration = .11f;
    private const float FireWindup = .22f;
    private const float FireRelease = .13f;
    private const float FireRecovery = .26f;
    private const float FireInterval = .85f;
    private const float FireSpeed = 16f;
    private const float FireRadius = .25f;
    private const float ImpactDuration = .20f;
    private const int MaxHealth = 100;
    private const int RubyHealing = 5;
    private const float RubyPickupRadius = .85f;
    private const int RightClickHealing = 20;
    private const float HealCooldown = 5f;
    private const int FireDamage = 30;
    private const float GroundImpactTime = 0.71f;
    private const float KnockoutScreenTime = 1.1f;

    private ForestFireCaster caster;
    private Camera view;
    private float lastHitAt = float.NegativeInfinity;
    private float stride;
    private bool flip;
    private bool casting;
    private float castStartedAt;
    private Quaternion castStartFacing;
    private Quaternion castFacing;
    private float releasedAt = float.PositiveInfinity;
    private float attackReadyAt;
    private float healReadyAt;
    private HunterAI castTarget;
    private Vector3? queuedAim;
    private HunterAI queuedTarget;
    private bool shotInFlight;
    private Vector3 shotPosition;
    private Vector3 shotOrigin;
    private Vector3 shotDirection;
    private float shotRemaining;
    private float impactAt = float.NegativeInfinity;
    private Vector3 impactPosition;
    private readonly RaycastHit[] fireHits = new RaycastHit[16];
    private HunterAI touchTarget;
    private Vector3? touchDestination;
    private float lastTouchAt = float.NegativeInfinity;
    private readonly List<Transform> rubies = new();
    private readonly List<Projectile> projectiles = new();
    private int health = MaxHealth;
    private int huntersRemaining;
    private HunterAI openingElf;
    private Transform openingRuby;
    private bool forestBattleStarted;
    private HunterAI mage;
    private HunterAI wangHao;
    private HunterAI supportMage;
    private ForestDuelHud duelHud;
    private ForestAudio sound;
    private ForestImpact impact;
    private string feedback;
    private float feedbackUntil;
    private Texture2D knockoutArtwork;
    private GUIStyle knockoutTitle;
    private GUIStyle knockoutSubtitle;
    private GUIStyle healthText;
    private float knockoutAt;
    private float knockoutShownAt;
    private Vector3 fallStartPosition;
    private Quaternion fallStartRotation;
    private bool landed;
    private bool showKnockout;

    public bool IsKnockedOut { get; private set; }
    public Vector3 ProjectileTarget => transform.position + Vector3.up * .98f;

    private void Awake()
    {
        Vector3 entrance = transform.position;
        entrance.z = OpeningSpawnDepth;
        transform.position = ClampToMeadow(entrance);
        followCamera.position = transform.position + CameraOffset;
        view = followCamera.GetComponent<Camera>();
        InitializeCharacter();
        caster = new ForestFireCaster(character, transform);
        duelHud = new ForestDuelHud();
        knockoutArtwork = Resources.Load<Texture2D>("Firelight/ArcadeKnockoutTitle");
    }

    private void InitializeCharacter()
    {
        if (character == null)
            character = transform.Find("A-pose character")?.GetComponent<SpriteRenderer>();
        if (character == null)
        {
            character = new GameObject("A-pose character").AddComponent<SpriteRenderer>();
            character.transform.SetParent(transform, false);
        }
        character.spriteSortPoint = SpriteSortPoint.Pivot;
        Sprite guardPose = Resources.Load<Sprite>("Firelight/FireCaster/RearGuard") ??
            Resources.Load<Sprite>("Firelight/FireCaster/BackAPose");
        if (guardPose != null)
            character.sprite = guardPose;

        Transform legacyRig = transform.Find("Body rig");
        if (legacyRig != null)
        {
            if (forestSurface == null)
                forestSurface = legacyRig.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial;
            legacyRig.gameObject.SetActive(false);
        }
        if (forestSurface == null)
            forestSurface = hunters[0].GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
        transform.rotation = BillboardFacing;
        character.transform.rotation = BillboardFacing;
        gameObject.name = "主人公";
    }

    private void Start()
    {
        sound = gameObject.AddComponent<ForestAudio>();
        if (GetComponent<Collider>() == null)
        {
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.up * .85f;
            capsule.height = 1.7f;
            capsule.radius = .62f;
        }
        var physicsBody = GetComponent<Rigidbody>();
        if (physicsBody == null)
            physicsBody = gameObject.AddComponent<Rigidbody>();
        physicsBody.isKinematic = true;
        physicsBody.useGravity = false;
        float groundHeight = transform.position.y;
        openingElf = hunters[^1];
        openingElf.MakeOpeningElf(new Vector3(1.8f, groundHeight, -8f));
        Vector3 scoutPost = new(-9f, groundHeight, 2.1f);
        var scout = Instantiate(hunters[1], scoutPost, Quaternion.identity);
        scout.gameObject.name = "Western forest elf";
        mage = SpawnMage(new Vector3(.5f, groundHeight, 12.5f), ForestMageLook.Forest);
        wangHao = SpawnMage(new Vector3(4.6f, groundHeight, 5.6f), ForestMageLook.WangHao);
        wangHao.transform.localScale *= 1.16f;
        supportMage = SpawnMage(new Vector3(-4.8f, groundHeight, 6.3f), ForestMageLook.Goblin);
        supportMage.SupportBoss(wangHao);
        Array.Resize(ref hunters, hunters.Length + 6);
        hunters[^6] = scout;
        hunters[^5] = mage;
        hunters[^4] = SpawnMage(new Vector3(-9.2f, groundHeight, 9.7f), ForestMageLook.Goblin);
        hunters[^3] = SpawnMage(new Vector3(8.1f, groundHeight, -5.8f), ForestMageLook.Goblin);
        hunters[^2] = supportMage;
        hunters[^1] = wangHao;
        huntersRemaining = hunters.Length;
        foreach (HunterAI hunter in hunters)
            if (hunter != openingElf)
                hunter.gameObject.SetActive(false);

        Material source = forestSurface;
        impact = new GameObject("Forest combat effects").AddComponent<ForestImpact>();
        impact.Initialize(source, groundHeight);
        Material rubyMaterial = new(source);
        rubyMaterial.SetColor("_BaseColor", new Color(0.9f, 0.035f, 0.23f));
        rubyMaterial.EnableKeyword("_EMISSION");
        rubyMaterial.SetColor("_EmissionColor", new Color(0.55f, 0.015f, 0.08f));
        Mesh ruby = ForestProps.CreateDiamond();
        openingRuby = CreateRuby(ruby, rubyMaterial, new Vector3(-1.45f, groundHeight + .30f, -11.25f), .45f);
        Vector2[] positions = { new(-5f, -6f), new(4.8f, -4f), new(-6.7f, 1f), new(6.5f, 3f), new(-4f, 8f), new(3.5f, 10f) };
        for (int group = 0; group < positions.Length; group++)
        {
            int count = 2 + group % 2;
            Vector2 center = positions[group];
            for (int index = 0; index < count; index++)
            {
                float angle = group * .61f + index * (2f * Mathf.PI / count);
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .40f;
                CreateRuby(ruby, rubyMaterial, new Vector3(point.x, groundHeight + .30f, point.y),
                    index == 0 ? .32f : .27f);
            }
        }
    }

    private Transform CreateRuby(Mesh mesh, Material material, Vector3 position, float size)
    {
        var gem = new GameObject("Ruby");
        gem.transform.position = position;
        gem.transform.localScale = Vector3.one * size;
        gem.AddComponent<MeshFilter>().sharedMesh = mesh;
        gem.AddComponent<MeshRenderer>().sharedMaterial = material;
        rubies.Add(gem.transform);
        return gem.transform;
    }

    private void Update()
    {
        if (IsKnockedOut)
        {
            AnimateKnockout();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Vector2 input = keyboard == null ? Vector2.zero : actiion.ReadArrowKeys(keyboard);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        float deltaTime = Time.deltaTime;
        float now = Time.time;

        var touch = Touchscreen.current?.primaryTouch;
        if (touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
            lastTouchAt = Time.unscaledTime;
        bool tapped = touch?.tap.wasPressedThisFrame == true;
        if (tapped)
        {
            lastTouchAt = Time.unscaledTime;
            queuedAim = null;
            queuedTarget = null;
            SetTouchCommand(touch.position.ReadValue());
        }

        Mouse mouse = Mouse.current;
        bool firePressed = mouse?.leftButton.wasPressedThisFrame == true && Time.unscaledTime - lastTouchAt > 0.5f;
        bool healPressed = mouse?.rightButton.wasPressedThisFrame == true;
        if (keyboard?.anyKey.wasPressedThisFrame == true || firePressed || healPressed || tapped)
            sound.StartMusic();

        if (direction.sqrMagnitude > 0f || firePressed)
        {
            touchTarget = null;
            touchDestination = null;
        }

        Vector3 movementStart = transform.position;
        float moveDistance = moveSpeed * deltaTime;
        if (direction.sqrMagnitude == 0f)
            direction = GetTouchMovement(now, ref moveDistance);

        if (direction.sqrMagnitude > 0f)
        {
            transform.position = ClampToMeadow(transform.position + direction * moveDistance);
            if (!casting)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(direction), 850f * deltaTime);
            }
        }

        UpdateRubies(now, movementStart);
        if (healPressed)
            TryHeal(now);
        if (firePressed)
        {
            var aimPlane = new Plane(Vector3.up, transform.position + Vector3.up * 1.2f);
            Ray ray = view.ScreenPointToRay(mouse.position.ReadValue());
            Vector3 aim = aimPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) - transform.position : Vector3.zero;
            aim.y = 0f;
            queuedAim = aim.sqrMagnitude > .25f ? aim : transform.forward;
            queuedTarget = EnemyAt(ray);
        }

        float impactAge = (now - impactAt) / ImpactDuration;
        caster.DrawImpact(impactPosition, BillboardFacing, impactAge);
        AdvanceFireball(now, deltaTime);
        AdvanceOpeningEncounter();
        UpdateCasting(now, direction);
    }

    private void AdvanceOpeningEncounter()
    {
        if (forestBattleStarted || openingElf.IsAlive || transform.position.z < ForestBattleDepth)
            return;
        forestBattleStarted = true;
        foreach (HunterAI hunter in hunters)
            if (hunter != openingElf)
                hunter.gameObject.SetActive(true);
    }

    private Quaternion BillboardFacing => Quaternion.LookRotation(
        Vector3.ProjectOnPlane(view.transform.forward, Vector3.up));

    private void UpdateCasting(float now, Vector3 movement)
    {
        if (casting && now >= releasedAt + FireRelease + FireRecovery)
            casting = false;
        if (!casting && now >= attackReadyAt && queuedAim.HasValue)
        {
            BeginCast(queuedAim.Value, now, queuedTarget);
            queuedAim = null;
            queuedTarget = null;
        }

        Quaternion billboard = BillboardFacing;
        bool walking = movement.sqrMagnitude > .001f;
        float age = now - castStartedAt;
        if (casting)
        {
            float turn = Mathf.SmoothStep(0f, 1f, age / FireTurnDuration);
            transform.rotation = Quaternion.Slerp(castStartFacing, castFacing, turn);
        }
        else if (!walking)
        {
            transform.rotation = billboard;
        }

        Transform portrait = character.transform;
        portrait.rotation = billboard;
        float facingDepth = Vector3.Dot(transform.forward, portrait.forward);
        float side = Vector3.Dot(transform.forward, portrait.right);
        if (Mathf.Abs(side) > .08f)
            flip = side < 0f;

        if (!casting)
        {
            stride = walking ? Mathf.Repeat(stride + Time.deltaTime * 10f, 4f) : 0f;
            float bob = walking ? Mathf.Abs(Mathf.Sin(stride * Mathf.PI * .5f)) * .055f : 0f;
            float hurt = 1f - Mathf.Clamp01((now - lastHitAt) / .25f);
            portrait.localPosition = Vector3.up * bob;
            portrait.rotation *= Quaternion.Euler(0f, 0f, hurt * 7f);
            caster.Rest(facingDepth, flip, walking ? (int)stride : -1);
            return;
        }

        portrait.localPosition = Vector3.zero;
        if (age < FireTurnDuration)
        {
            caster.Rest(facingDepth, flip, -1);
            return;
        }

        bool fromBehind = facingDepth > .55f;
        bool attackFlip = side < -.08f;
        float windupAge = age - FireTurnDuration;
        if (windupAge < FireWindup)
        {
            caster.Charge(windupAge / FireWindup, attackFlip, fromBehind);
            return;
        }

        if (float.IsPositiveInfinity(releasedAt))
        {
            releasedAt = now;
            LaunchFireball(caster.Release(attackFlip, fromBehind));
        }
        float releaseAge = now - releasedAt;
        if (releaseAge < FireRelease)
            caster.ShowRelease(releaseAge / FireRelease, fromBehind);
        else
            caster.Recover((releaseAge - FireRelease) / FireRecovery, fromBehind);
    }

    private void SetTouchCommand(Vector2 screenPoint)
    {
        if (!view.pixelRect.Contains(screenPoint))
            return;

        Ray ray = view.ScreenPointToRay(screenPoint);
        touchTarget = EnemyAt(ray);
        touchDestination = null;
        var ground = new Plane(Vector3.up, transform.position);
        if (touchTarget == null && ground.Raycast(ray, out float groundDistance))
            touchDestination = ClampToMeadow(ray.GetPoint(groundDistance));
    }

    private HunterAI EnemyAt(Ray ray)
    {
        HunterAI nearest = null;
        float nearestDistance = view.farClipPlane;
        foreach (HunterAI hunter in hunters)
        {
            if (!hunter.IsAlive || !hunter.isActiveAndEnabled)
                continue;

            if (EnemyBounds(hunter).IntersectRay(ray, out float distance) && distance < nearestDistance)
            {
                nearest = hunter;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    private Vector3 GetTouchMovement(float now, ref float moveDistance)
    {
        if (touchTarget != null && touchTarget.IsAlive && touchTarget.isActiveAndEnabled)
        {
            if (casting)
                return Vector3.zero;

            Vector3 offset = touchTarget.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= TouchFireDistance * TouchFireDistance)
            {
                if (now >= attackReadyAt)
                    BeginCast(offset, now, touchTarget);
                return Vector3.zero;
            }

            moveDistance = Mathf.Min(moveDistance, offset.magnitude - TouchFireDistance);
            return offset.normalized;
        }

        touchTarget = null;
        if (!touchDestination.HasValue)
            return Vector3.zero;

        Vector3 remaining = touchDestination.Value - transform.position;
        remaining.y = 0f;
        float distance = remaining.magnitude;
        if (distance <= moveDistance)
            touchDestination = null;
        moveDistance = Mathf.Min(moveDistance, distance);
        return remaining.normalized;
    }

    private Vector3 ClampToMeadow(Vector3 point)
    {
        point.x = Mathf.Clamp(point.x, -meadowBounds.x, meadowBounds.x);
        point.z = Mathf.Clamp(point.z, -meadowBounds.y, meadowBounds.y);
        return point;
    }

    private static Bounds EnemyBounds(HunterAI hunter)
    {
        Vector3 scale = hunter.transform.lossyScale;
        Vector3 center = hunter.transform.position + Vector3.up * (1.35f * scale.y);
        return new Bounds(center, Vector3.Scale(new Vector3(1.25f, 2.7f, 1.25f), scale));
    }

    private void BeginCast(Vector3 aim, float now, HunterAI focused)
    {
        if (focused != null && focused.IsAlive)
            aim = Vector3.ProjectOnPlane(focused.transform.position - transform.position, Vector3.up);
        castStartFacing = transform.rotation;
        castFacing = aim.sqrMagnitude > .0001f ? Quaternion.LookRotation(aim) : castStartFacing;

        castTarget = focused;
        casting = true;
        castStartedAt = now;
        releasedAt = float.PositiveInfinity;
        attackReadyAt = now + FireInterval;
    }

    private void LaunchFireball(Vector3 origin)
    {
        Vector3 destination = transform.position + transform.forward * FireReach + Vector3.up * 1.35f;
        if (castTarget != null && castTarget.IsAlive && castTarget.isActiveAndEnabled)
        {
            Vector3 center = EnemyBounds(castTarget).center;
            Vector3 offset = center - transform.position;
            Vector3 flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (flatOffset.sqrMagnitude <= FireReach * FireReach)
                destination = center;
        }

        shotOrigin = shotPosition = origin;
        Vector3 path = destination - origin;
        shotRemaining = path.magnitude;
        shotDirection = shotRemaining > .01f ? path / shotRemaining : transform.forward;
        shotInFlight = true;
        caster.DrawShot(origin, origin, shotDirection, BillboardFacing, Time.time);
        sound.PlayFire();
    }

    private void AdvanceFireball(float now, float deltaTime)
    {
        if (!shotInFlight)
            return;

        float travel = Mathf.Min(shotRemaining, deltaTime * FireSpeed);
        if (travel <= 0f && shotRemaining > 0f)
            return;

        var ray = new Ray(shotPosition, shotDirection);
        float nearestDistance = travel;
        bool collided = false;
        int obstacleCount = Physics.SphereCastNonAlloc(ray, FireRadius, fireHits, travel,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < obstacleCount; i++)
        {
            RaycastHit hit = fireHits[i];
            if (hit.distance > nearestDistance || hit.collider.transform.IsChildOf(transform))
                continue;
            nearestDistance = hit.distance;
            collided = true;
        }

        HunterAI victim = null;
        foreach (HunterAI hunter in hunters)
        {
            if (!hunter.IsAlive || !hunter.isActiveAndEnabled)
                continue;
            Bounds hitBox = EnemyBounds(hunter);
            hitBox.Expand(FireRadius * 2f);
            bool overlaps = hitBox.Contains(shotPosition);
            if (!hitBox.IntersectRay(ray, out float distance) && !overlaps)
                continue;
            distance = overlaps ? 0f : distance;
            if (distance > nearestDistance || (collided && distance >= nearestDistance))
                continue;
            nearestDistance = distance;
            victim = hunter;
            collided = true;
        }

        shotPosition = ray.GetPoint(nearestDistance);
        shotRemaining -= nearestDistance;
        if (collided || shotRemaining <= .001f)
        {
            shotInFlight = false;
            caster.HideShot();
            if (victim != null)
            {
                if (victim.TakeFireDamage(FireDamage))
                {
                    huntersRemaining--;
                    sound.PlayHunterBurn();
                }
                ShowFeedback("Fireball hit");
            }
            impactPosition = shotPosition;
            impactAt = now;
            caster.DrawImpact(impactPosition, BillboardFacing, 0f);
            return;
        }

        float trailLength = Mathf.Min(.85f, Vector3.Distance(shotOrigin, shotPosition));
        caster.DrawShot(shotPosition, shotPosition - shotDirection * trailLength, shotDirection, BillboardFacing, now);
    }

    public void LaunchProjectile(Projectile prefab, Transform owner, Vector3 start, Material source, float speed, ProjectileKind kind)
    {
        if (!IsKnockedOut)
            projectiles.Add(Projectile.Launch(prefab, owner, this, start, ProjectileTarget, source, speed, kind));
    }

    private HunterAI SpawnMage(Vector3 position, ForestMageLook appearance)
    {
        HunterAI enemy = Instantiate(hunters[0], position, Quaternion.Euler(0f, 180f, 0f));
        enemy.MakeMage(appearance);
        return enemy;
    }

    internal Vector3 SpaceHunterStep(HunterAI moving, Vector3 step)
    {
        const float defaultSpacing = 1.45f;
        const float duelSpacing = 3.4f;
        HunterAI duelPartner = moving == wangHao ? supportMage : moving == supportMage ? wangHao : null;
        Vector3 position = moving.transform.position;
        float maximum = step.magnitude;
        foreach (HunterAI other in hunters)
        {
            if (other == moving || !other.IsAlive || !other.isActiveAndEnabled)
                continue;
            float spacing = other == duelPartner ? duelSpacing : defaultSpacing;
            Vector3 offset = Vector3.ProjectOnPlane(position - other.transform.position, Vector3.up);
            float separation = offset.magnitude;
            if (separation < .001f || separation > spacing + maximum)
                continue;
            Vector3 away = offset / separation;
            float inward = Vector3.Dot(step, away);
            if (inward < 0f)
                step -= away * Mathf.Min(0f, inward + Mathf.Max(0f, separation - spacing));
            if (separation < spacing)
                step += away * (maximum * (1f - separation / spacing));
        }
        return Vector3.ClampMagnitude(step, maximum);
    }

    public void ForgetProjectile(Projectile projectile)
    {
        projectiles.Remove(projectile);
    }

    public void TakeProjectileHit(int damage, ProjectileKind kind, Vector3 point, Vector3 travel)
    {
        string label = kind switch
        {
            ProjectileKind.Arrow => "Arrow hit",
            ProjectileKind.SpiritFire => "Spirit fire hit",
            ProjectileKind.BlueFire => "Blue fireball hit",
            _ => "Stone hit"
        };
        ApplyHunterHit(damage, point, travel, label);
    }

    public void TakeMeleeHit(Vector3 attacker, int damage)
    {
        Vector3 travel = ProjectileTarget - attacker;
        ApplyHunterHit(damage, ProjectileTarget - travel.normalized * .58f, travel, "Hunter strike");
    }

    private void ApplyHunterHit(int damage, Vector3 point, Vector3 travel, string label)
    {
        if (IsKnockedOut)
            return;

        health = Mathf.Max(0, health - damage);
        lastHitAt = Time.time;
        sound.PlayPlayerHurt();
        impact.Blood(point, travel);
        if (health > 0)
        {
            ShowFeedback($"{label}  -{damage} HP");
            return;
        }

        IsKnockedOut = true;
        casting = false;
        queuedAim = null;
        queuedTarget = null;
        shotInFlight = false;
        caster.HideHandFire();
        caster.HideShot();
        caster.DrawImpact(Vector3.zero, BillboardFacing, 1f);
        knockoutAt = Time.time;
        fallStartPosition = character.transform.position;
        fallStartRotation = character.transform.rotation;
        sound.StopMusic();
        foreach (Projectile projectile in projectiles.ToArray())
            projectile.Remove();
        foreach (HunterAI hunter in hunters)
            hunter.StopAttack();
    }

    private void AnimateKnockout()
    {
        if (showKnockout)
            return;

        float age = Time.time - knockoutAt;
        float stagger = Mathf.Clamp01(age / 0.22f);
        float drop = Mathf.Clamp01((age - 0.22f) / (GroundImpactTime - 0.22f));
        float roll = Mathf.Lerp(-12f * stagger, 86f, drop * drop);
        float settle = age > GroundImpactTime ? Mathf.Sin((age - GroundImpactTime) * 24f) *
            Mathf.Exp(-(age - GroundImpactTime) * 11f) : 0f;
        character.transform.rotation = fallStartRotation * Quaternion.Euler(7f * drop, 9f * drop, roll - settle * 6f);
        character.transform.position = fallStartPosition - view.transform.right * (.55f * drop) + Vector3.up * (.12f * settle);

        if (!landed && age >= GroundImpactTime)
        {
            landed = true;
            impact.Dust(transform.position + transform.right * 0.7f + Vector3.up * 0.13f);
            sound.PlayGroundImpact();
        }
        if (age >= KnockoutScreenTime)
        {
            showKnockout = true;
            knockoutShownAt = Time.unscaledTime;
            sound.PlayKnockout();
        }
    }

    private void UpdateRubies(float now, Vector3 movementStart)
    {
        Vector3 travel = transform.position - movementStart;
        travel.y = 0f;
        float travelSquared = travel.sqrMagnitude;
        int totalRestored = 0;
        foreach (Transform gem in rubies)
        {
            if (!gem.gameObject.activeSelf)
                continue;
            gem.rotation = Quaternion.Euler(0f, now * 65f, 0f);
            Vector3 offset = gem.position - movementStart;
            offset.y = 0f;
            float alongPath = travelSquared > 0f ? Mathf.Clamp01(Vector3.Dot(offset, travel) / travelSquared) : 0f;
            if (health == MaxHealth || (offset - travel * alongPath).sqrMagnitude > RubyPickupRadius * RubyPickupRadius)
                continue;
            int restored = Mathf.Min(RubyHealing, MaxHealth - health);
            health += restored;
            totalRestored += restored;
            gem.gameObject.SetActive(false);
        }
        if (totalRestored > 0)
        {
            sound.PlayRuby();
            ShowFeedback($"+{totalRestored} HP");
        }
    }

    private void TryHeal(float now)
    {
        if (health == MaxHealth)
            return;
        if (now < healReadyAt)
        {
            ShowFeedback($"Heal ready in {Mathf.CeilToInt(healReadyAt - now)}s");
            return;
        }

        int restored = Mathf.Min(RightClickHealing, MaxHealth - health);
        health += restored;
        healReadyAt = now + HealCooldown;
        sound.PlayRuby();
        ShowFeedback($"+{restored} HP");
    }

    private void ShowFeedback(string text)
    {
        feedback = text;
        feedbackUntil = Time.time + 1.25f;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        if (showKnockout)
        {
            DrawKnockout();
            return;
        }

        bool touchControls = Mouse.current == null || Time.unscaledTime - lastTouchAt < .5f;
        string status = forestBattleStarted
            ? (huntersRemaining == 0 ? "The forest is safe" : $"{huntersRemaining} enemies left")
            : (openingElf.IsAlive ? "Defeat the elf" : "Follow the path");
        duelHud.Draw(health, MaxHealth, forestBattleStarted ? wangHao : null, status, touchControls,
            Time.time < feedbackUntil ? feedback : null);
        foreach (HunterAI hunter in hunters)
        {
            if (!hunter.IsAlive || !hunter.isActiveAndEnabled)
                continue;
            float height = hunter.IsBoss ? 3.4f : hunter == mage ? 3.25f : 2.95f;
            Vector3 point = hunter.transform.position + Vector3.up * height;
            DrawHeadHealth(point, hunter.Health, hunter.MaxHealth, hunter.IsHealing || hunter.IsReceivingHealing, hunter.IsHealing);
        }
        if (!forestBattleStarted && openingRuby.gameObject.activeSelf)
            duelHud.DrawRubyHint(view.WorldToScreenPoint(openingRuby.position), touchControls);
    }

    private void DrawHeadHealth(Vector3 point, int current, int maximum, bool healing, bool chanting)
    {
        Vector3 screen = view.WorldToScreenPoint(point);
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height)
            return;
        float uiScale = Mathf.Clamp(Screen.height / 720f, .82f, 1.4f);
        float width = 65f * uiScale;
        float healthHeight = 23f * uiScale;
        float margin = width * .5f;
        float center = Mathf.Clamp(screen.x, margin, Screen.width - margin);
        float x = center - width * .5f;
        float y = Mathf.Max(2f, Screen.height - screen.y - healthHeight);

        healthText ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        healthText.fontSize = Mathf.RoundToInt(11f * uiScale);
        Color previous = GUI.color;
        GUI.color = new Color(.08f, .046f, .029f, .95f);
        GUI.DrawTexture(new Rect(x, y, width, healthHeight), Texture2D.whiteTexture);
        GUI.color = healing ? new Color(.39f, 1f, .51f) : new Color(.97f, .49f, .3f);
        GUI.DrawTexture(new Rect(x + 3f, y + healthHeight - 5f * uiScale,
            (width - 6f) * current / maximum, 3f * uiScale), Texture2D.whiteTexture);
        GUI.color = new Color(1f, .94f, .82f);
        GUI.Label(new Rect(x, y - uiScale, width, healthHeight - 4f * uiScale), $"{current}/{maximum}", healthText);
        if (chanting)
            GUI.Label(new Rect(x - 14f, y - healthHeight, width + 28f, healthHeight), "Chanting", healthText);
        GUI.color = previous;
    }

    private void DrawKnockout()
    {
        knockoutSubtitle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        knockoutSubtitle.fontSize = Mathf.Clamp(Screen.height / 36, 14, 32);
        float age = Time.unscaledTime - knockoutShownAt;
        Color previous = GUI.color;
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.color = new Color(.065f, .022f, .027f, .83f);
        GUI.DrawTexture(screen, Texture2D.whiteTexture);
        GUI.color = new Color(1f, .43f, .16f, .17f * (1f - Mathf.Clamp01(age / .14f)));
        GUI.DrawTexture(screen, Texture2D.whiteTexture);

        GUI.color = Color.white;
        if (knockoutArtwork != null)
        {
            float aspect = (float)knockoutArtwork.width / knockoutArtwork.height;
            float width = Mathf.Min(Screen.width * .60f, Screen.height * .33f * aspect);
            float stamp = 1f + .24f * Mathf.Exp(-age * 12f) * Mathf.Cos(age * 17f);
            width *= stamp;
            float height = width / aspect;
            var title = new Rect((Screen.width - width) * .5f, Screen.height * .43f - height * .5f, width, height);
            GUI.DrawTexture(title, knockoutArtwork, ScaleMode.ScaleToFit, true);
        }
        else
        {
            knockoutTitle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            knockoutTitle.fontSize = Mathf.Clamp(Screen.height / 6, 44, 100);
            GUI.color = new Color(1f, .48f, .28f);
            GUI.Label(new Rect(0f, Screen.height * .28f, Screen.width, Screen.height * .3f), "KO", knockoutTitle);
        }

        GUI.color = new Color(1f, .91f, .77f);
        GUI.Label(new Rect(0f, Screen.height * .66f, Screen.width, 44f), "GAME OVER", knockoutSubtitle);
        GUI.color = previous;
    }

    private void LateUpdate()
    {
        Vector3 target = transform.position + CameraOffset;
        float blend = 1f - Mathf.Exp(-Time.deltaTime * 7f);
        followCamera.position = Vector3.Lerp(followCamera.position, target, blend);
    }
}
