using UnityEngine;
using UnityEngine.Rendering;

internal sealed class ForestFireCaster
{
    private const string ResourceFolder = "Firelight/FireCaster/";
    private static readonly Vector2 RearGatherCenter = new(.539f, 1.634f);
    private static readonly Vector2 SideGatherCenter = new(-.226f, 1.568f);
    private static readonly Vector2 ReleasePalm = new(.816f, 2.161f);
    private static readonly Vector2 RecoverPalm = new(-.517f, 1.75f);
    private static readonly Vector2 RearReleasePalm = new(.838f, 2.267f);
    private static readonly Vector2 RearRecoverPalm = new(.569f, 1.839f);

    private readonly SpriteRenderer body;
    private readonly Sprite rearGuard;
    private readonly Sprite sideGuard;
    private readonly Sprite rearGather = Load("RearGather");
    private readonly Sprite sideGather = Load("SideGather");
    private readonly Sprite release = Load("Release");
    private readonly Sprite recover = Load("Recover");
    private readonly Sprite rearRelease = Load("RearRelease");
    private readonly Sprite rearRecover = Load("RearRecover");
    private readonly Sprite[] walk = { Load("Walk1"), Load("Walk2"), Load("Walk3"), Load("Walk4") };
    private readonly Sprite orb = Load("Fireball");
    private readonly Sprite flash = Load("Flash");
    private readonly Sprite charge = Load("Charge");
    private readonly Sprite vortex = Load("ChargeVortex");
    private readonly SpriteRenderer handGlow;
    private readonly SpriteRenderer handFire;
    private readonly SpriteRenderer fireball;
    private readonly SpriteRenderer trail;
    private readonly SpriteRenderer impact;

    public ForestFireCaster(SpriteRenderer body, Transform player)
    {
        this.body = body;
        rearGuard = Load("RearGuard") ?? body.sprite;
        sideGuard = Load("SideGuard") ?? rearGuard;
        body.gameObject.AddComponent<SortingGroup>();
        handGlow = MakeLayer(body.transform, "Hand aura", 1);
        handFire = MakeLayer(body.transform, "Hand fire", 2);
        fireball = MakeLayer(player, "Red fireball", 1);
        trail = MakeLayer(player, "Fireball trail", 0);
        impact = MakeLayer(player, "Fireball impact", 1);
    }

    public void Rest(float facingDepth, bool flip, int walkFrame)
    {
        bool fromBehind = facingDepth > .55f;
        Sprite idle = fromBehind ? rearGuard : sideGuard;
        body.sprite = walkFrame >= 0 ? walk[walkFrame] ?? idle : idle;
        body.flipX = walkFrame >= 0 ? flip : !fromBehind && !flip;
        HideHandFire();
    }

    public void Charge(float progress, bool flip, bool fromBehind)
    {
        body.sprite = fromBehind ? rearGather ?? rearGuard : sideGather ?? sideGuard;
        // The side guard and gather artwork faces left; the release artwork faces right.
        body.flipX = fromBehind ? flip : !flip;
        float weight = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 center = Hand(fromBehind ? RearGatherCenter : SideGatherCenter);
        PlaceHand(handGlow, vortex ?? charge, center, Mathf.Lerp(.38f, 1.08f, weight),
            Mathf.Lerp(.55f, 1f, weight), progress * 62f);
        PlaceHand(handFire, orb, center, Mathf.Lerp(.10f, .24f, weight), Mathf.Lerp(.45f, .8f, weight), -progress * 22f);
    }

    public Vector3 Release(bool flip, bool fromBehind)
    {
        body.sprite = fromBehind ? rearRelease ?? release : release;
        body.flipX = flip;
        return body.transform.TransformPoint(Hand(CastReleasePalm(fromBehind)));
    }

    public void ShowRelease(float progress, bool fromBehind)
    {
        handGlow.enabled = false;
        PlaceHand(handFire, flash, Hand(CastReleasePalm(fromBehind)), Mathf.Lerp(.42f, .92f, progress), 1f - progress, 0f);
    }

    public void Recover(float progress, bool fromBehind)
    {
        bool useRearPose = fromBehind && rearRecover != null;
        body.sprite = useRearPose ? rearRecover : recover;
        handFire.enabled = false;
        float fade = 1f - progress;
        float retract = Mathf.SmoothStep(0f, 1f, progress * 1.8f);
        Vector2 palm = useRearPose ? RearRecoverPalm : RecoverPalm;
        Vector3 hand = Hand(Vector2.Lerp(CastReleasePalm(fromBehind), palm, retract));
        PlaceHand(handGlow, charge, hand, .34f * fade, .48f * fade, 60f * progress);
    }

    public void HideHandFire()
    {
        handGlow.enabled = false;
        handFire.enabled = false;
    }

    public void DrawShot(Vector3 position, Vector3 behind, Vector3 direction, Quaternion facing, float now)
    {
        DrawWorld(fireball, orb, position, facing, .76f, 1f, now * 380f);
        float length = Vector3.Distance(position, behind);
        if (length < .04f)
        {
            trail.enabled = false;
            return;
        }
        Vector3 projected = Quaternion.Inverse(facing) * direction;
        float angle = Mathf.Atan2(projected.y, projected.x) * Mathf.Rad2Deg;
        DrawWorld(trail, orb, Vector3.Lerp(position, behind, .58f), facing, .58f + length, .58f, angle);
        Vector3 scale = trail.transform.localScale;
        scale.y *= .34f;
        trail.transform.localScale = scale;
    }

    public void HideShot()
    {
        fireball.enabled = false;
        trail.enabled = false;
    }

    public void DrawImpact(Vector3 position, Quaternion facing, float progress)
    {
        if (progress >= 1f)
        {
            impact.enabled = false;
            return;
        }
        DrawWorld(impact, flash, position, facing, Mathf.Lerp(.62f, 1.65f, progress), 1f - progress, 0f);
    }

    private Vector3 Hand(Vector2 palm) => new(body.flipX ? -palm.x : palm.x, palm.y, -.055f);

    private Vector2 CastReleasePalm(bool fromBehind) => fromBehind && rearRelease != null ? RearReleasePalm : ReleasePalm;

    private static void PlaceHand(SpriteRenderer layer, Sprite sprite, Vector3 position, float width, float alpha, float angle)
    {
        layer.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
        SetAppearance(layer, sprite, width, alpha);
    }

    private static void DrawWorld(SpriteRenderer layer, Sprite sprite, Vector3 position, Quaternion facing,
        float width, float alpha, float angle)
    {
        layer.transform.SetPositionAndRotation(position, facing * Quaternion.Euler(0f, 0f, angle));
        SetAppearance(layer, sprite, width, alpha);
    }

    private static void SetAppearance(SpriteRenderer layer, Sprite sprite, float width, float alpha)
    {
        layer.enabled = sprite != null && alpha > 0f;
        if (!layer.enabled)
            return;
        layer.sprite = sprite;
        layer.color = new Color(1f, 1f, 1f, alpha);
        layer.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
    }

    private static SpriteRenderer MakeLayer(Transform parent, string name, int order)
    {
        var layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.sortingOrder = order;
        layer.enabled = false;
        return layer;
    }

    private static Sprite Load(string name) => Resources.Load<Sprite>(ResourceFolder + name);
}
