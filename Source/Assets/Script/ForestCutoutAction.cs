using UnityEngine;

internal sealed class ForestCutoutAction
{
    private const string ResourceFolder = "Firelight/Actions/";
    private const float BowRecovery = .42f;
    private const float SpellRecovery = .68f;
    private static readonly Vector2 GoblinWindupTip = new(-.337f, 2.458f);
    private static readonly Vector2 GoblinReleaseTip = new(1.709f, 2.204f);
    private static readonly Vector2 GoblinRecoverTip = new(-.495f, 2.293f);
    private static readonly Vector2 WangHaoWindupPalm = new(-.066f, 1.369f);
    private static readonly Vector2 WangHaoReleasePalm = new(1.023f, 1.874f);
    private static readonly Vector2 WangHaoRecoverPalm = new(-.135f, 1.424f);

    private readonly SpriteRenderer body;
    private readonly SpriteRenderer aura;
    private readonly SpriteRenderer effect;
    private readonly SpriteRenderer blessing;
    private readonly Sprite idle;
    private readonly Sprite[] poses;
    private readonly Sprite orb;
    private readonly Sprite ring;
    private readonly Sprite flash;
    private readonly bool mage;
    private readonly bool launchFromReleaseFrame;
    private readonly Vector2[] anchors;
    private readonly Sprite chantPose;
    private readonly Sprite healAura;

    private Vector3 target;
    private Vector3 releasePosition;
    private float progress;
    private float recoveryStarted = float.NegativeInfinity;
    private int frame = -1;
    private bool windingUp;
    private bool released;
    private bool healing;
    private float lastHealPulse;
    private float healInterval;
    private float receivedHealAt = float.NegativeInfinity;

    public ForestCutoutAction(SpriteRenderer body, SpriteRenderer aura, SpriteRenderer effect, bool mage,
        ForestMageLook appearance, SpriteRenderer blessing)
    {
        this.body = body;
        this.aura = aura;
        this.effect = effect;
        this.blessing = blessing;
        this.mage = mage;
        idle = body.sprite;
        if (appearance != ForestMageLook.Forest)
        {
            bool goblin = appearance == ForestMageLook.Goblin;
            string folder = goblin ? "Firelight/Goblin/" : "Firelight/WangHao/";
            Sprite windup = Resources.Load<Sprite>(folder + "Windup");
            poses = new[] { windup, windup, Resources.Load<Sprite>(folder + "Release"),
                Resources.Load<Sprite>(folder + "Recover") };
            launchFromReleaseFrame = true;
            orb = LoadGoblin("BlueFireball");
            ring = Load("SpiritRing");
            flash = LoadGoblin("BlueFlash");
            chantPose = goblin ? LoadGoblin("Chant") : null;
            healAura = LoadGoblin("HealAura");
            anchors = goblin
                ? new[] { GoblinWindupTip, GoblinWindupTip, GoblinReleaseTip, GoblinRecoverTip }
                : new[] { WangHaoWindupPalm, WangHaoWindupPalm, WangHaoReleasePalm, WangHaoRecoverPalm };
            return;
        }
        string prefix = mage ? "Mage" : "Archer";
        poses = new[] { Load(prefix + "Prepare"), Load(prefix + "Attack"), Load(prefix + "Release"), Load(prefix + "Recover") };
        orb = mage ? Load("SpiritOrb") : null;
        ring = mage ? Load("SpiritRing") : null;
        flash = Load(mage ? "SpiritFlash" : "BowFlash");
        anchors = mage
            ? new[] { new Vector2(.069f, 1.841f), new Vector2(.397f, 1.983f), new Vector2(.385f, 1.967f), new Vector2(.296f, 1.557f) }
            : new[] { new Vector2(-.507f, 1.333f), new Vector2(-.664f, 1.535f), new Vector2(-.684f, 1.475f), new Vector2(-.016f, 1.116f) };
    }

    public void WindUp(float progress, Vector3 target)
    {
        this.progress = progress;
        this.target = target;
        windingUp = true;
        recoveryStarted = float.NegativeInfinity;
        SetFrame(progress >= .55f ? 1 : 0);
    }

    public bool TryGetReleasePosition(Camera camera, out Vector3 position)
    {
        position = default;
        if (!windingUp || frame < 0 || poses[frame] == null || camera == null)
            return false;
        if (launchFromReleaseFrame && poses[2] != null)
            SetFrame(2);
        UpdateFacing(camera);
        position = body.transform.TransformPoint(Anchor(frame, -.065f));
        return true;
    }

    public void Recover(Vector3? launchPosition)
    {
        if (!windingUp)
            return;
        windingUp = false;
        released = launchPosition.HasValue;
        releasePosition = launchPosition.GetValueOrDefault();
        recoveryStarted = Time.time;
        SetFrame(released ? 2 : 3);
        if (!released)
        {
            aura.enabled = false;
            effect.enabled = false;
        }
    }

    public void Tick(float now)
    {
        if (healing || windingUp || frame < 0)
            return;
        float duration = mage ? SpellRecovery : BowRecovery;
        float elapsed = now - recoveryStarted;
        if (elapsed >= duration)
            SetFrame(-1);
        else if (elapsed >= duration * .38f)
            SetFrame(3);
    }

    public void RenderFor(Camera camera)
    {
        UpdateFacing(camera);
        aura.enabled = false;
        effect.enabled = false;
        if (blessing != null)
        {
            float t = Mathf.Clamp01((Time.time - receivedHealAt) / .48f);
            blessing.enabled = false;
            if (t < 1f)
                Place(blessing, healAura, new Vector3(0f, .67f + t * 1.1f, -.11f),
                    1.45f + t * .5f, .75f * (1f - t), t * 90f);
        }
        if (healing)
        {
            float t = Mathf.Clamp01((Time.time - lastHealPulse) / healInterval);
            Place(aura, healAura, new Vector3(0f, .70f + t * .55f, -.075f),
                Mathf.Lerp(1.20f, 2.08f, t), .72f * (1f - t) + .16f, Time.time * 50f);
            Place(effect, healAura, new Vector3(0f, .22f + t * 1.63f, -.095f),
                .44f + t * .48f, Mathf.Sin(Mathf.PI * t) * .75f, -Time.time * 90f);
            return;
        }
        if (frame < 0)
            return;
        if (windingUp && mage)
        {
            float charge = Mathf.SmoothStep(0f, 1f, progress);
            var position = Anchor(frame, -.065f);
            Place(aura, ring, position, .24f + .42f * charge, .28f + .55f * charge, Time.time * -95f);
            position.z -= .015f;
            Place(effect, orb, position, .12f + .31f * charge, .48f + .52f * charge, 0f);
            return;
        }
        if (windingUp || !released)
            return;

        float elapsed = Time.time - recoveryStarted;
        float flashTime = mage ? .22f : .16f;
        if (elapsed < flashTime)
        {
            float t = Mathf.Clamp01(elapsed / flashTime);
            Vector3 position = body.transform.InverseTransformPoint(releasePosition);
            Place(aura, flash, position, Mathf.Lerp(.24f, mage ? .72f : .54f, t), 1f - t, 0f);
        }
        if (mage && elapsed > .08f)
        {
            float t = Mathf.Clamp01((elapsed - .08f) / (SpellRecovery - .08f));
            Vector3 hand = Anchor(frame, -.085f);
            Vector3 outward = (target - releasePosition).normalized * .62f;
            Vector3 returnOrigin = body.transform.InverseTransformPoint(releasePosition + outward);
            Vector3 point = Vector3.Lerp(returnOrigin, hand, t);
            point.y += .16f * Mathf.Sin(Mathf.PI * t);
            Place(effect, orb, point, Mathf.Lerp(.27f, .09f, t), Mathf.Sin(Mathf.PI * t), t * 125f);
        }
    }

    public void BeginHealing(float now, float tickInterval)
    {
        healing = true;
        healInterval = tickInterval;
        windingUp = false;
        frame = -1;
        body.sprite = chantPose ?? idle;
        PulseHealing(now);
    }

    public void PulseHealing(float now) => lastHealPulse = now;

    public void ReceiveHealing(float now) => receivedHealAt = now;

    public void EndHealing()
    {
        healing = false;
        body.sprite = idle;
        aura.enabled = false;
        effect.enabled = false;
    }

    private void UpdateFacing(Camera camera)
    {
        if (frame < 0)
        {
            body.flipX = false;
            return;
        }
        float side = Vector3.Dot(target - body.transform.position, camera.transform.right);
        body.flipX = Mathf.Abs(side) > .08f && (mage ? side < 0f : side > 0f);
    }

    private Vector3 Anchor(int pose, float depth)
    {
        Vector2 point = anchors[pose];
        return new Vector3(body.flipX ? -point.x : point.x, point.y, depth);
    }

    private void SetFrame(int index)
    {
        if (frame == index)
            return;
        frame = index;
        body.sprite = index < 0 ? idle : poses[index] ?? idle;
    }

    private static void Place(SpriteRenderer layer, Sprite sprite, Vector3 position, float width, float opacity, float angle)
    {
        if (sprite == null)
            return;
        layer.sprite = sprite;
        layer.enabled = true;
        layer.color = new Color(1f, 1f, 1f, opacity);
        layer.transform.localPosition = position;
        layer.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        layer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static Sprite Load(string name) => Resources.Load<Sprite>(ResourceFolder + name);

    private static Sprite LoadGoblin(string name) => Resources.Load<Sprite>("Firelight/Goblin/" + name);
}
