using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ForestAnimeHunter : MonoBehaviour
{
    [SerializeField] private Transform rig;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;
    [SerializeField] private Transform weaponArm;
    [SerializeField] private int palette;
    [Header("C:/human 風格的原創角色資源")]
    [SerializeField] private GameObject visualModelPrefab;
    [SerializeField] private Texture2D outfitBaseMap;
    [SerializeField] private string outfitRendererName;
    [SerializeField, Min(0)] private int outfitMaterialSlot;

    public Transform SupportArm { get; private set; }
    public Transform Chest { get; private set; }
    public Transform StaffTip { get; private set; }
    public Animator VisualAnimator { get; private set; }
    public bool UsesCharacterCutout => characterCutout != null;

    private readonly List<MeshRenderer> originalRenderers = new();
    private readonly List<GameObject> parts = new();
    private readonly List<Material> materials = new();
    private readonly List<Mesh> tailoredMeshes = new();
    private Mesh oval;
    private Mesh tapered;
    private bool preview;
    private SpriteRenderer characterCutout;
    private ForestCutoutAction cutoutAction;

    public void ShowAttackWindup(float progress, Vector3 target) => cutoutAction?.WindUp(progress, target);

    public void RecoverAttack(Vector3? launchPosition = null) => cutoutAction?.Recover(launchPosition);

    public void BeginHealing(float now, float tickInterval) => cutoutAction?.BeginHealing(now, tickInterval);

    public void PulseHealing(float now) => cutoutAction?.PulseHealing(now);

    public void EndHealing() => cutoutAction?.EndHealing();

    public void ReceiveHealing(float now) => cutoutAction?.ReceiveHealing(now);

    public bool TryGetAttackOrigin(out Vector3 position)
    {
        position = default;
        Camera camera = Camera.main;
        if (cutoutAction == null || camera == null)
            return false;
        OrientPortrait(camera);
        return cutoutAction.TryGetReleasePosition(camera, out position);
    }

    private void LateUpdate() => cutoutAction?.Tick(Time.time);

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.IsPlaying(gameObject))
            UnityEditor.EditorApplication.delayCall += ShowEditorPreview;
#endif
    }

#if UNITY_EDITOR
    private void ShowEditorPreview()
    {
        if (this != null && isActiveAndEnabled && gameObject.scene.isLoaded &&
            !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            Build(false, true);
    }
#endif

    internal bool SetGameplayLook(bool mage, ForestMageLook appearance)
    {
        return Build(mage, false, appearance);
    }

    private bool Build(bool mage, bool editPreview, ForestMageLook appearance = ForestMageLook.Forest)
    {
        Clear();
        if (rig == null || leftLeg == null || rightLeg == null || weaponArm == null)
            return false;

        Sprite cutout = null;
        if (appearance == ForestMageLook.Goblin)
            cutout = Resources.Load<Sprite>("Firelight/Goblin/Idle");
        else if (appearance == ForestMageLook.WangHao)
            cutout = Resources.Load<Sprite>("Firelight/WangHao/APose");
        if (cutout == null && (mage || visualModelPrefab == null))
            cutout = Resources.Load<Sprite>(mage ? "Firelight/ForestMage" : "Firelight/ElfArcher");
        Shader shader = Resources.Load<Shader>("Firelight/HunterCel");
        if (shader == null || !shader.isSupported)
            shader = rig.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial?.shader;
        if (shader == null && cutout == null)
            return false;

        preview = editPreview;
        foreach (MeshRenderer old in rig.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!old.forceRenderingOff)
            {
                old.forceRenderingOff = true;
                originalRenderers.Add(old);
            }
        }
        if (visualModelPrefab != null && !mage)
        {
            GameObject replacement = Instantiate(visualModelPrefab, rig, false);
            if (preview)
                replacement.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            parts.Add(replacement);
            VisualAnimator = replacement.GetComponentInChildren<Animator>();
            Chest = replacement.transform;
            ApplyOutfitTexture(replacement);
            return true;
        }
        Transform gripArm = CreatePart(rig, "Left shoulder");
        SupportArm = gripArm;
        SupportArm.localPosition = new Vector3(-.35f, 1.71f, .03f);

        if (cutout != null)
        {
            string portraitName = appearance != ForestMageLook.Forest
                ? $"{appearance} cutout" : mage ? "Forest mage cutout" : "Elf archer cutout";
            Transform portrait = CreatePart(rig, portraitName);
            portrait.localPosition = Vector3.up * .025f;
            characterCutout = portrait.gameObject.AddComponent<SpriteRenderer>();
            characterCutout.sprite = cutout;
            Chest = portrait;
            if (!preview)
            {
                portrait.gameObject.AddComponent<SortingGroup>();
                SpriteRenderer aura = CreatePart(portrait, "Attack aura layer").gameObject.AddComponent<SpriteRenderer>();
                SpriteRenderer effect = CreatePart(portrait, "Attack detail layer").gameObject.AddComponent<SpriteRenderer>();
                aura.sortingOrder = 1;
                effect.sortingOrder = 2;
                aura.enabled = false;
                effect.enabled = false;
                SpriteRenderer blessing = null;
                if (appearance == ForestMageLook.WangHao)
                {
                    blessing = CreatePart(portrait, "Received healing layer").gameObject.AddComponent<SpriteRenderer>();
                    blessing.sortingOrder = 3;
                    blessing.enabled = false;
                    Transform electricLayer = CreatePart(portrait, "Full-body lightning");
                    electricLayer.localPosition = Vector3.up * 1.44f;
                    electricLayer.gameObject.AddComponent<ForestLightning>()
                        .Configure(.62f, 2.65f, .07f, sortingOrder: 4);
                }
                cutoutAction = new ForestCutoutAction(characterCutout, aura, effect, mage, appearance, blessing);
            }
            if (mage)
            {
                StaffTip = CreatePart(weaponArm, "Spell launch point");
                StaffTip.localPosition = new Vector3(.219f, .51f, .351f);
            }
            RenderPipelineManager.beginCameraRendering += FaceCamera;
            return true;
        }

        oval = MakeLoft("Smooth contour", new[] { -1f, -.88f, -.63f, -.32f, 0f, .32f, .63f, .88f, 1f },
            new[] { .015f, .47f, .77f, .95f, 1f, .95f, .77f, .47f, .015f });
        tapered = MakeLoft("Tailored contour", new[] { -1f, -.88f, -.42f, .3f, .78f, 1f },
            new[] { .51f, .78f, .94f, 1f, .83f, .2f });
        Mesh leaf = Tailor("Pointed leaf", new[] { -1f, -.69f, -.24f, .26f, .67f, 1f },
            new[] { .03f, .63f, 1f, .78f, .38f, .015f }, new[] { .02f, .56f, 1f, .72f, .36f, .015f });
        Mesh coat = Tailor("Long forest tunic",
            new[] { -1f, -.86f, -.55f, -.3f, -.02f, .28f, .59f, .82f, 1f },
            new[] { .97f, 1f, .84f, .72f, .75f, .83f, .98f, .94f, .43f },
            new[] { .91f, 1f, .9f, .77f, .83f, .94f, 1f, .93f, .55f }, .043f);
        Mesh mantle = MakeMantle();
        tailoredMeshes.Add(mantle);
        float[] headHeights = { -1f, -.92f, -.8f, -.66f, -.51f, -.4f, -.29f, -.18f, -.08f, .04f,
            .16f, .26f, .34f, .44f, .57f, .68f, .8f, .95f, 1.09f, 1.18f };
        float[] headWidths = { .24f, .47f, .59f, .68f, .74f, .8f, .86f, .9f, .93f, .96f,
            .97f, .96f, .94f, .94f, .95f, .93f, .89f, .77f, .54f, .04f };
        float[] headDepths = { .32f, .63f, .77f, .82f, .86f, .89f, .91f, .93f, .95f, .94f,
            .91f, .88f, .88f, .91f, .93f, .91f, .85f, .73f, .47f, .04f };
        Mesh face = Tailor("Face with chin, jaw and cheekbones", headHeights, headWidths, headDepths, segments: 64);
        Mesh hairCap = MakeSweptHair(mage, headHeights, headWidths, headDepths);
        tailoredMeshes.Add(hairCap);
        Color32[] fabricColors = { new(38, 100, 71, 255), new(42, 112, 98, 255), new(57, 105, 52, 255), new(39, 86, 89, 255) };
        Color32[] mantles = { new(63, 105, 52, 255), new(44, 98, 71, 255), new(79, 105, 45, 255), new(38, 94, 69, 255) };
        int style = Mathf.Clamp(palette, 0, fabricColors.Length - 1);
        Material cloth = Tone(shader, mage ? new Color32(39, 58, 111, 255) : fabricColors[style], .94f, .078f);
        Material cape = Tone(shader, mage ? new Color32(54, 53, 104, 255) : mantles[style], .93f, .071f);
        Material foliage = Tone(shader, mage ? new Color32(181, 165, 75, 255) : new Color32(126, 163, 67, 255), .69f, .03f);
        Material skin = Tone(shader, mage ? new Color32(147, 123, 105, 255) : new Color32(206, 162, 130, 255), .78f, .009f);
        Material skinShade = Tone(shader, new Color32(131, 87, 65, 255), .82f, 0f);
        Color32[] hairColors = { new(50, 40, 31, 255), new(61, 47, 35, 255), new(43, 40, 35, 255), new(65, 51, 41, 255) };
        Material hair = Tone(shader, mage ? new Color32(54, 52, 49, 255) : hairColors[style], .59f, .012f);
        if (hair.HasProperty("_Hair"))
            hair.SetFloat("_Hair", mage ? 1f : .65f);
        Material pants = Tone(shader, new Color32(45, 43, 39, 255), .94f, .061f);
        Material leather = Tone(shader, mage ? new Color32(56, 48, 41, 255) : new Color32(81, 60, 46, 255), .84f, .068f);
        Material boots = Tone(shader, new Color32(110, 88, 70, 255), .86f, .067f);
        Material strap = Tone(shader, new Color32(117, 85, 60, 255), .85f, .056f);
        Material trim = Tone(shader, new Color32(145, 112, 73, 255), .58f, .021f);
        Material linen = Tone(shader, mage ? new Color32(75, 68, 57, 255) : new Color32(154, 132, 98, 255), .96f, .063f);
        Material sclera = Tone(shader, new Color32(207, 186, 162, 255), .55f, 0f);
        Material iris = Tone(shader, new Color32(83, 65, 47, 255), .56f, 0f);

        // 使用既有骨架驅動步伐和拉弓動作。
        Part(rig, "Long dark green tunic", coat, cloth, new(0f, 1.405f, 0f), new(.343f, .552f, .209f));
        Part(rig, "Forest green shoulder mantle", mantle, cape, Vector3.zero, Vector3.one);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                Part(rig, "Layered mantle leaf", leaf, layer == 1 ? cloth : foliage,
                    new(side * (.19f + layer * .079f), 1.884f - layer * .057f, .205f - layer * .04f),
                    new(.075f, .145f, .027f), new(-18f, side * 18f, side * (59f - layer * 15f)));
            }
        }
        Part(rig, "Tall folded collar", oval, cape, new(0f, 1.984f, -.021f), new(.164f, .113f, .139f), new(-12f, 0f, 0f));
        Part(rig, "Weathered collar lip", oval, strap, new(-.026f, 2.004f, .091f), new(.146f, .016f, .047f), new(0f, 0f, -11f));
        Part(rig, "Neck", oval, skin, new(0f, 2.016f, .044f), new(.082f, .117f, .076f));
        Part(rig, "Double-wrapped belt", oval, leather, new(0f, 1.262f, .005f), new(.279f, .038f, .183f));
        Part(rig, "Belt stitching", oval, strap, new(0f, 1.276f, .005f), new(.278f, .013f, .186f));
        Part(rig, "Small aged buckle", oval, trim, new(-.015f, 1.271f, .183f), new(.044f, .038f, .015f));
        Part(rig, "Loose belt end", tapered, strap, new(.071f, 1.102f, .193f), new(.025f, .156f, .016f), new(0f, 0f, -8f));
        for (int knot = 0; knot < 5; knot++)
        {
            float y = 1.695f - knot * .083f;
            Part(rig, "Tunic front cross-lacing", oval, strap, new(.005f, y, .208f),
                new(.012f, .041f, .013f), new(0f, 0f, knot % 2 == 0 ? 38f : -38f));
        }

        MakeLeg(leftLeg, pants, boots, linen);
        MakeLeg(rightLeg, pants, boots, linen);
        Part(SupportArm, "Left sleeve", oval, cloth, new(-.015f, -.14f, -.018f), new(.126f, .252f, .148f), new(0f, 0f, -16f));
        Part(SupportArm, "Linen undersleeve", tapered, linen, new(-.089f, -.368f, .066f), new(.082f, .224f, .079f), new(-15f, 0f, -6f));
        Part(SupportArm, "Left leather bracer", tapered, leather, new(-.096f, -.472f, .096f), new(.082f, .108f, .077f), new(-15f, 0f, -7f));
        Part(SupportArm, "Left hand", oval, skin, new(-.113f, -.591f, .12f), new(.089f, .112f, .073f));
        Part(weaponArm, "Right sleeve", oval, cloth, new(.055f, -.158f, .052f), new(.132f, .215f, .137f), new(-15f, 0f, 13f));
        Part(weaponArm, "Gathered linen sleeve", tapered, linen, new(.143f, -.315f, .187f), new(.085f, .177f, .086f), new(-40f, 0f, 23f));
        Part(weaponArm, "Right arm guard", tapered, leather, new(.177f, -.397f, .256f), new(.092f, .124f, .093f), new(-40f, 0f, 23f));
        Part(weaponArm, "Right wrist cuff", oval, strap, new(.195f, -.468f, .305f), new(.086f, .05f, .082f), new(-33f, 0f, 19f));
        Part(weaponArm, "Right hand", oval, skin, new(.219f, -.526f, .351f), new(.093f, .091f, .082f));

        if (mage)
            SculptFace(face);
        Vector3 headPosition = new(0f, 2.213f, .053f);
        Vector3 headScale = new(.166f, .199f, .154f);
        Part(rig, "Sculpted head", face, skin, headPosition, headScale);
        Part(rig, "Swept hair and tapered temples", hairCap, hair, headPosition, headScale);
        Part(rig, "Nose bridge", tapered, skin, new(0f, 2.206f, .202f), new(.025f, .055f, .022f), new(-15f, 0f, 0f));
        Part(rig, "Nose tip", oval, skin, new(.001f, 2.176f, .219f), new(.028f, .02f, .022f));
        Part(rig, "Closed mouth", oval, skinShade, new(0f, 2.12f, .177f), new(.037f, .004f, .01f), new(0f, 0f, 2f));
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 earAngle = new(-11f, side * 13f, -side * 53f);
            Part(rig, "Pointed elf ear", leaf, skin, new(side * .206f, 2.248f, .045f), new(.041f, .111f, .034f), earAngle);
            Part(rig, "Inner ear", leaf, skinShade, new(side * .213f, 2.251f, .069f), new(.022f, .073f, .014f), earAngle);
            Part(rig, "Upper eyelid", oval, skinShade, new(side * .065f, 2.253f, .186f), new(.033f, .015f, .014f), new(0f, side * 15f, side * 4f));
            Part(rig, "Eye white", oval, sclera, new(side * .065f, 2.251f, .197f), new(.027f, .009f, .009f), new(0f, side * 15f, side * 4f));
            Part(rig, "Iris", oval, iris, new(side * .063f, 2.252f, .204f), new(.009f, .01f, .006f));
            Part(rig, "Brow", oval, hair, new(side * .067f, 2.278f, .187f), new(.035f, .008f, .013f), new(0f, side * 13f, side * 9f));
        }

        if (mage)
        {
            Part(rig, "Long worn cloak", coat, cape, new(0f, 1.14f, -.199f), new(.405f, .85f, .14f), new(-5f, 0f, 0f));
            Part(rig, "Mage hat brim", oval, cape, new(0f, 2.46f, .041f), new(.38f, .038f, .32f), new(-5f, 0f, 8f));
            Mesh crown = Tailor("Pointed hat crown", new[] { -1f, -.76f, -.14f, .58f, 1f },
                new[] { 1f, .97f, .63f, .25f, .01f }, new[] { .91f, .87f, .64f, .23f, .01f });
            Part(rig, "Mage hat crown", crown, cloth, new(-.04f, 2.68f, .005f), new(.205f, .25f, .184f), new(-9f, 0f, 10f));
            Part(rig, "Mage hat band", oval, trim, new(-.015f, 2.51f, .023f), new(.198f, .03f, .17f), new(-5f, 0f, 8f));
            Part(weaponArm, "Wooden staff", tapered, strap, new(.219f, -.52f, .351f), new(.043f, .93f, .043f));
            Part(weaponArm, "Staff crystal setting", oval, trim, new(.219f, .39f, .351f), new(.094f, .095f, .084f));
            Material crystal = Tone(shader, new Color32(98, 222, 255, 255), .24f, 0f);
            StaffTip = Part(weaponArm, "Spirit crystal", leaf, crystal,
                new(.219f, .51f, .351f), new(.085f, .152f, .07f));
        }
        else
        {
            AddQuiver(leather, strap, linen);
        }
        var chest = new GameObject("Chest and mantle");
        if (preview)
            chest.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
        Chest = chest.transform;
        Chest.SetParent(rig, false);
        Chest.localPosition = Vector3.up * 1.28f;
        foreach (GameObject part in parts)
            if (part.transform.parent == rig && part.transform != gripArm)
                part.transform.SetParent(Chest, true);
        parts.Add(chest);
        return true;
    }

    private void AddQuiver(Material leather, Material strap, Material linen)
    {
        Part(rig, "Diagonal quiver strap", tapered, leather, new(-.035f, 1.59f, .211f), new(.044f, .36f, .035f), new(0f, 0f, -34f));
        Part(rig, "Leather quiver", tapered, leather, new(-.24f, 1.65f, -.25f), new(.092f, .47f, .1f), new(-9f, 0f, 24f));
        for (int arrow = 0; arrow < 5; arrow++)
        {
            Vector3 point = new(-.47f + arrow * .031f, 2.18f + arrow % 3 * .064f, -.31f - arrow % 2 * .045f);
            Part(rig, "Quiver arrow shaft", tapered, strap, point, new(.009f, .22f, .009f), new(-9f, 0f, 24f));
            Part(rig, "Quiver arrow fletching", tapered, linen, point + new Vector3(-.059f, .141f, -.026f),
                new(.023f, .071f, .014f), new(-9f, 0f, 24f));
        }
    }

    private void MakeLeg(Transform leg, Material pants, Material leather, Material linen)
    {
        Part(leg, "Slim charcoal trousers", tapered, pants, new(0f, -.424f, .014f), new(.135f, .465f, .128f));
        Part(leg, "Tapered lower trouser leg", tapered, pants, new(0f, -.78f, .005f), new(.093f, .237f, .104f));
        Part(leg, "Gathered knee fabric", oval, pants, new(.003f, -.606f, .087f), new(.103f, .08f, .06f));
        Part(leg, "Short leather boot", tapered, leather, new(0f, -1.012f, .024f), new(.099f, .157f, .105f));
        Part(leg, "Worn leather boot toe", oval, leather, new(0f, -1.067f, .09f), new(.112f, .101f, .178f));
        Part(leg, "Boot sole", oval, pants, new(0f, -1.145f, .09f), new(.119f, .038f, .183f));
        for (int lace = 0; lace < 3; ++lace)
            Part(leg, "Boot lacing", oval, linen, new(.002f, -1.011f + lace * .027f, .137f - lace * .014f),
                new(.051f, .007f, .013f), new(0f, 0f, lace % 2 == 0 ? 13f : -13f));
    }

    private void ApplyOutfitTexture(GameObject replacement)
    {
        if (outfitBaseMap == null || string.IsNullOrWhiteSpace(outfitRendererName))
            return;
        foreach (Renderer renderer in replacement.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name != outfitRendererName || outfitMaterialSlot >= renderer.sharedMaterials.Length)
                continue;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties, outfitMaterialSlot);
            properties.SetTexture("_BaseMap", outfitBaseMap);
            renderer.SetPropertyBlock(properties, outfitMaterialSlot);
        }
    }

    private Material Tone(Shader shader, Color color, float roughness, float grain)
    {
        var material = new Material(shader) { hideFlags = HideFlags.DontSave };
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 1f - roughness);
        if (material.HasProperty("_Roughness"))
        {
            material.SetFloat("_Roughness", roughness);
            material.SetFloat("_Grain", grain);
        }
        materials.Add(material);
        return material;
    }

    private Transform Part(Transform parent, string label, Mesh mesh, Material material,
        Vector3 point, Vector3 scale, Vector3 angles = default)
    {
        Transform part = CreatePart(parent, label);
        part.localPosition = point;
        part.localRotation = Quaternion.Euler(angles);
        part.localScale = scale;
        part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    private Transform CreatePart(Transform parent, string label)
    {
        var part = new GameObject(label);
        if (preview)
            part.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
        part.transform.SetParent(parent, false);
        parts.Add(part);
        return part.transform;
    }

    private Mesh Tailor(string label, float[] heights, float[] widths, float[] depths = null, float folds = 0f, int segments = 24)
    {
        Mesh mesh = MakeLoft(label, heights, widths, depths, folds, segments);
        tailoredMeshes.Add(mesh);
        return mesh;
    }

    private static Mesh MakeLoft(string label, float[] heights, float[] widths, float[] depths = null, float folds = 0f, int segments = 24)
    {
        var vertices = new Vector3[heights.Length * segments + 2];
        var triangles = new List<int>();
        for (int row = 0; row < heights.Length; row++)
        {
            for (int side = 0; side < segments; side++)
            {
                float a = 2f * Mathf.PI * side / segments;
                int current = row * segments + side;
                float depth = depths == null ? widths[row] : depths[row];
                float pleat = 1f + folds * Mathf.Cos(a * 8f + heights[row] * 1.4f);
                vertices[current] = new Vector3(Mathf.Sin(a) * widths[row] * pleat, heights[row], Mathf.Cos(a) * depth * pleat);
                if (row == 0)
                    continue;
                int next = row * segments + (side + 1) % segments;
                triangles.Add(current);
                triangles.Add(current - segments);
                triangles.Add(next);
                triangles.Add(next);
                triangles.Add(current - segments);
                triangles.Add(next - segments);
            }
        }
        int bottom = vertices.Length - 2;
        int top = bottom + 1;
        int topRing = (heights.Length - 1) * segments;
        vertices[bottom] = Vector3.up * heights[0];
        vertices[top] = Vector3.up * heights[^1];
        for (int side = 0; side < segments; side++)
        {
            int next = (side + 1) % segments;
            triangles.Add(bottom);
            triangles.Add(next);
            triangles.Add(side);
            triangles.Add(top);
            triangles.Add(topRing + side);
            triangles.Add(topRing + next);
        }
        var mesh = new Mesh { name = label, vertices = vertices, triangles = triangles.ToArray(), hideFlags = HideFlags.DontSave };
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void SculptFace(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = vertices[i];
            float x = Mathf.Abs(point.x);
            float y = point.y;
            float orbit = Contour(x, .33f, .18f) * Contour(y, .29f, .13f);
            float cheek = Contour(x, .53f, .24f) * Contour(y, .04f, .19f);
            float nose = Contour(x, 0f, .14f) * Contour(y, .045f, .30f);
            float noseTip = Contour(x, 0f, .20f) * Contour(y, -.10f, .102f);
            float brow = Contour(x, .32f, .30f) * Contour(y, .45f, .085f);
            float lips = Contour(x, 0f, .27f) * Contour(y, -.43f, .13f);
            point.z += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .92f, point.z)) *
                (-.15f * orbit + .115f * cheek + .21f * nose + .14f * noseTip + .080f * brow + .055f * lips);
            vertices[i] = point;
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private static float Contour(float value, float center, float width)
    {
        float distance = (value - center) / width;
        return Mathf.Exp(-distance * distance);
    }

    private static Mesh MakeSweptHair(bool mage, float[] heights, float[] widths, float[] depths)
    {
        const int sectors = 72;
        const int rows = 15;
        const int ring = sectors + 1;
        var vertices = new Vector3[rows * ring + 1];
        var uv = new Vector2[vertices.Length];
        var triangles = new List<int>((rows - 1) * sectors * 6 + sectors * 3);
        for (int row = 0; row < rows; row++)
        {
            float along = row / (float)rows;
            for (int col = 0; col <= sectors; col++)
            {
                float turn = col / (float)sectors;
                float angle = turn * 2f * Mathf.PI;
                float side = Mathf.Sin(angle);
                float front = Mathf.Cos(angle);
                float temple = Mathf.Pow(Mathf.Abs(side), 6f);
                float hairline = Mathf.Lerp(1.98f, mage ? 1.02f : 1.11f, (front + 1f) * .5f);
                hairline += .22f * temple + (mage ? .055f : .18f) * side * Mathf.Max(front, 0f);
                hairline += .032f * Mathf.Sin(angle * 9f - .7f) * Mathf.Max(0f, front);
                float polar = hairline * (1f - along);
                float sweep = angle - Mathf.Sin(Mathf.PI * along) * (.28f + .14f * front);
                float ridges = (Mathf.Sin(angle * 31f + along * 4f) + .4f * Mathf.Sin(angle * 19f - along * 6f)) *
                    Mathf.Sin(polar) * .016f;
                float radius = Mathf.Sin(polar) * (1f + ridges);
                int id = row * ring + col;
                Vector3 point = new(radius * Mathf.Sin(sweep) * 1.09f,
                    .13f + Mathf.Cos(polar) * (mage ? 1.25f : 1.30f) + ridges,
                    -.07f + radius * Mathf.Cos(sweep) * 1.2f - along * .12f);
                vertices[id] = FitHairToHead(point, heights, widths, depths);
                uv[id] = new(turn, along);
                if (row == 0 || col == sectors)
                    continue;
                int previous = id - ring;
                triangles.Add(id);
                triangles.Add(previous);
                triangles.Add(id + 1);
                triangles.Add(id + 1);
                triangles.Add(previous);
                triangles.Add(previous + 1);
            }
        }
        int top = vertices.Length - 1;
        vertices[top] = new Vector3(0f, mage ? 1.38f : 1.43f, -.19f);
        uv[top] = new Vector2(.5f, 1f);
        for (int col = 0; col < sectors; col++)
        {
            triangles.Add(top);
            triangles.Add((rows - 1) * ring + col);
            triangles.Add((rows - 1) * ring + col + 1);
        }
        var mesh = new Mesh { name = "Combed hair", vertices = vertices, uv = uv, triangles = triangles.ToArray(), hideFlags = HideFlags.DontSave };
        mesh.RecalculateNormals();
        Vector3[] normals = mesh.normals;
        for (int row = 0; row < rows; row++)
        {
            int start = row * ring;
            normals[start] = normals[start + sectors] = (normals[start] + normals[start + sectors]).normalized;
        }
        mesh.normals = normals;
        return mesh;
    }

    private static Vector3 FitHairToHead(Vector3 point, float[] heights, float[] widths, float[] depths)
    {
        if (point.y >= heights[^1])
            return point;
        int upper = 1;
        while (heights[upper] < point.y)
            upper++;
        float blend = Mathf.InverseLerp(heights[upper - 1], heights[upper], point.y);
        float x = point.x / Mathf.Lerp(widths[upper - 1], widths[upper], blend);
        float z = point.z / Mathf.Lerp(depths[upper - 1], depths[upper], blend);
        float distance = Mathf.Sqrt(x * x + z * z);
        float clearance = Mathf.Max(1f, 1.067f / Mathf.Max(distance, .001f));
        return new Vector3(point.x * clearance, point.y, point.z * clearance);
    }

    private static Mesh MakeMantle()
    {
        const int sectors = 40;
        const int rings = 7;
        var vertices = new Vector3[rings * (sectors + 1)];
        var triangles = new List<int>((rings - 1) * sectors * 6);
        for (int row = 0; row < rings; row++)
        {
            float drape = row / (float)(rings - 1);
            for (int col = 0; col <= sectors; col++)
            {
                float angle = Mathf.Lerp(.035f, 2f * Mathf.PI - .035f, col / (float)sectors);
                float across = Mathf.Sin(angle);
                float front = Mathf.Cos(angle);
                float fold = Mathf.Sin(angle * 11f + drape * .9f) * .035f * drape;
                float width = Mathf.Lerp(.125f, .472f, drape);
                float depth = Mathf.Lerp(.117f, .269f, drape);
                float hem = 1.69f + .043f * Mathf.Abs(across) - .038f * Mathf.Max(0f, front);
                float y = Mathf.Lerp(2.02f, hem, drape) + fold;
                int index = row * (sectors + 1) + col;
                vertices[index] = new Vector3(across * (width + fold), y, front * (depth + fold) - .024f);
                if (row == 0 || col == sectors)
                    continue;
                int previous = index - sectors - 1;
                triangles.Add(previous);
                triangles.Add(index);
                triangles.Add(previous + 1);
                triangles.Add(previous + 1);
                triangles.Add(index);
                triangles.Add(index + 1);
            }
        }
        var mesh = new Mesh
        {
            name = "Folded cloth shoulder cape",
            vertices = vertices,
            triangles = triangles.ToArray(),
            hideFlags = HideFlags.DontSave
        };
        mesh.RecalculateNormals();
        return mesh;
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ShowEditorPreview;
#endif
        Clear();
    }

    private void FaceCamera(ScriptableRenderContext context, Camera camera)
    {
        if (characterCutout == null)
            return;
        OrientPortrait(camera);
        cutoutAction?.RenderFor(camera);
    }

    private void OrientPortrait(Camera camera)
    {
        Transform portrait = characterCutout.transform;
        Vector3 facing = camera.orthographic
            ? camera.transform.forward
            : portrait.position - camera.transform.position;
        Vector3 up = rig.up;
        facing = Vector3.ProjectOnPlane(facing, up);
        if (facing.sqrMagnitude > .0001f)
            portrait.rotation = Quaternion.LookRotation(facing, up);
    }

    private void Clear()
    {
        RenderPipelineManager.beginCameraRendering -= FaceCamera;
        cutoutAction = null;
        characterCutout = null;
        foreach (MeshRenderer original in originalRenderers)
            if (original != null)
                original.forceRenderingOff = false;
        originalRenderers.Clear();
        foreach (GameObject part in parts)
            Free(part);
        parts.Clear();
        SupportArm = null;
        Chest = null;
        StaffTip = null;
        VisualAnimator = null;
        foreach (Material material in materials)
            Free(material);
        materials.Clear();
        foreach (Mesh mesh in tailoredMeshes)
            Free(mesh);
        tailoredMeshes.Clear();
        Free(oval);
        Free(tapered);
        oval = null;
        tapered = null;
    }

    private void Free(Object item)
    {
        if (item == null)
            return;
        if (preview || !Application.isPlaying)
            DestroyImmediate(item);
        else
            Destroy(item);
    }
}
