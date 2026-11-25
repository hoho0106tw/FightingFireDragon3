using UnityEngine;

public enum ProjectileKind { Stone, Arrow, SpiritFire, BlueFire }

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class Projectile : MonoBehaviour
{
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[12];
    [Header("命中設定")]
    [SerializeField, Min(1)] private int damage = 5;
    [SerializeField, Min(.1f)] private float lifetime = 4f;
    [SerializeField] private LayerMask contactLayers = Physics.DefaultRaycastLayers;

    private Rigidbody body;
    private SphereCollider hitbox;
    private ForestDinosaur target;
    private Transform owner;
    private ProjectileKind kind;
    private Material spiritGlow;
    private Transform fireballSprite;
    private bool spent;
    private Vector3 lastCenter;

    public static Projectile Launch(Projectile prefab, Transform owner, ForestDinosaur target, Vector3 origin,
        Vector3 aim, Material material, float speed, ProjectileKind kind)
    {
        Vector3 displacement = aim - origin;
        Vector3 forward = displacement.sqrMagnitude > .0001f ? displacement.normalized : owner.forward;
        Quaternion rotation = kind == ProjectileKind.Stone ? Quaternion.identity : Quaternion.LookRotation(forward);
        Projectile shot;
        if (kind == ProjectileKind.SpiritFire || kind == ProjectileKind.BlueFire)
        {
            shot = CreateSpiritFire(origin, rotation, material, kind == ProjectileKind.BlueFire);
        }
        else if (prefab != null)
        {
            shot = Instantiate(prefab, origin, rotation);
        }
        else
        {
            // 本場景可直接使用程式建立的低多邊形外觀，也支援 Inspector 指定的 Prefab。
            bool arrow = kind == ProjectileKind.Arrow;
            GameObject root = ForestStone.CreateVisual(origin, rotation, material, arrow);
            shot = root.AddComponent<Projectile>();
            root.GetComponent<SphereCollider>().radius = arrow ? .055f : .12f;
        }

        shot.body = shot.GetComponent<Rigidbody>();
        shot.hitbox = shot.GetComponent<SphereCollider>();
        shot.target = target;
        shot.owner = owner;
        shot.kind = kind;
        shot.hitbox.isTrigger = true;
        shot.lastCenter = shot.Center;
        shot.body.isKinematic = false;
        shot.body.useGravity = kind == ProjectileKind.Stone;
        shot.body.linearDamping = 0f;
        shot.body.interpolation = RigidbodyInterpolation.Interpolate;

        // 忽略發射者自己的 Collider，避免投射物還沒離開手部就被消耗。
        foreach (Collider part in owner.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(shot.hitbox, part);

        if (kind != ProjectileKind.Stone)
        {
            shot.body.linearVelocity = forward * speed;
        }
        else
        {
            // 先估算石頭飛到目標平面的時間，再由位移公式求向前和向上的初速。
            // VelocityChange 不受 Prefab 質量影響。往後的下墜交給 Unity 重力。
            Vector3 horizontal = Vector3.ProjectOnPlane(displacement, Vector3.up);
            float gravity = Mathf.Max(.01f, -Physics.gravity.y);
            float risingTime = Mathf.Sqrt(Mathf.Max(0f, -2f * displacement.y / gravity)) + .09f;
            float flightTime = Mathf.Max(.58f, Mathf.Max(horizontal.magnitude / speed, risingTime));
            shot.body.AddForce(displacement / flightTime - Physics.gravity * (.5f * flightTime), ForceMode.VelocityChange);
            shot.body.angularVelocity = new Vector3(5f, 7f, 2f);
        }
        Destroy(shot.gameObject, shot.lifetime);
        return shot;
    }

    private static Projectile CreateSpiritFire(Vector3 origin, Quaternion rotation, Material material, bool blueFire)
    {
        var root = new GameObject(blueFire ? "Blue fireball" : "Spirit firebolt");
        root.transform.SetPositionAndRotation(origin, rotation);
        var shot = root.AddComponent<Projectile>();
        shot.damage = 12;
        root.GetComponent<SphereCollider>().radius = .19f;
        Sprite orb = blueFire ? Resources.Load<Sprite>("Firelight/Goblin/BlueFireball") : null;
        if (orb != null)
        {
            var core = new GameObject("Blue flame layer").AddComponent<SpriteRenderer>();
            core.sprite = orb;
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * (.83f / orb.bounds.size.x);
            shot.fireballSprite = core.transform;
            shot.OrientFireball();
        }
        else
        {
            ForestHunterDetail.AddShape(root.transform, "Spirit fire core", ForestStone.SharedMesh,
                material, Vector3.zero, new Vector3(.39f, .39f, .53f));
        }

        Shader glow = Resources.Load<Shader>("Firelight/LightningGlow");
        if (glow != null)
        {
            shot.spiritGlow = new Material(glow);
            shot.spiritGlow.SetColor("_BaseColor", new Color(.29f, .56f, 1f, .37f));
            if (orb == null)
                ForestHunterDetail.AddShape(root.transform, "Spirit fire halo", ForestStone.SharedMesh,
                    shot.spiritGlow, Vector3.back * .14f, new Vector3(.57f, .57f, .86f));
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = shot.spiritGlow;
            trail.time = .23f;
            trail.startWidth = .39f;
            trail.endWidth = 0f;
            trail.minVertexDistance = .06f;
        }
        return shot;
    }

    private void LateUpdate()
    {
        if (fireballSprite != null)
            OrientFireball();
    }

    private void OrientFireball()
    {
        Camera camera = Camera.main;
        if (camera != null)
            fireballSprite.rotation = camera.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * 210f);
    }

    private void FixedUpdate()
    {
        if (spent || body == null)
            return;

        // 高速箭有機會在兩次物理更新之間跨過薄 Collider。掃過實際走過的一段補判。
        if (TrySweep())
            return;
        lastCenter = Center;

        Vector3 velocity = body.linearVelocity;
        if (kind != ProjectileKind.Stone && velocity.sqrMagnitude > .0001f)
            body.MoveRotation(Quaternion.LookRotation(velocity));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (spent || body == null || !CanContact(other))
            return;
        // 若同一步同時碰到樹幹與玩家，採用路徑上較近的接觸點。
        if (!TrySweep())
            ResolveContact(other, other.ClosestPoint(Center));
    }

    // 使用 Rigidbody 的物理座標，避免 Interpolate 造成畫面座標落後於碰撞位置。
    private Vector3 Center => body.position + body.rotation * Vector3.Scale(hitbox.center, transform.lossyScale);

    private bool TrySweep()
    {
        Vector3 travel = Center - lastCenter;
        float length = travel.magnitude;
        if (length < .0001f)
            return false;

        Vector3 scale = transform.lossyScale;
        float radius = hitbox.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        RaycastHit nearest = default;
        float closest = float.PositiveInfinity;
        Vector3 direction = travel / length;
        RaycastHit[] hits = hitBuffer;
        int count = Physics.SphereCastNonAlloc(lastCenter, radius, direction, hits, length,
            contactLayers, QueryTriggerInteraction.Collide);
        if (count == hits.Length)
        {
            // 緩衝區滿時重查，否則無法保證保留下來的是最近的障礙物。
            hits = Physics.SphereCastAll(lastCenter, radius, direction, length, contactLayers, QueryTriggerInteraction.Collide);
            count = hits.Length;
        }
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance < closest && CanContact(hit.collider) &&
                !Physics.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer))
            {
                nearest = hit;
                closest = hit.distance;
            }
        }
        if (nearest.collider == null)
            return false;
        Vector3 point = closest > 0f ? nearest.point : nearest.collider.ClosestPoint(lastCenter);
        ResolveContact(nearest.collider, point);
        return true;
    }

    private bool CanContact(Collider other)
    {
        if (other.attachedRigidbody == body || (owner != null && other.transform.IsChildOf(owner)) ||
            (contactLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;
        // 其他投射物、警戒區等 Trigger 不會擋箭，但玩家可使用 Trigger Collider。
        return !other.isTrigger || IsTarget(other);
    }

    private bool IsTarget(Collider other) => target != null && other.GetComponentInParent<ForestDinosaur>() == target;

    private void ResolveContact(Collider other, Vector3 point)
    {
        Vector3 velocity = body.linearVelocity;
        Remove(); // 先標記已處理，避免恐龍有多個 Collider 時重複扣血。
        if (IsTarget(other))
            target.TakeProjectileHit(damage, kind, point, velocity);
    }

    public void Remove()
    {
        if (spent)
            return;
        spent = true;
        body.detectCollisions = false;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (target != null)
            target.ForgetProjectile(this);
        if (spiritGlow != null)
            Destroy(spiritGlow);
    }
}
