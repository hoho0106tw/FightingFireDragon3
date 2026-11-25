using UnityEngine;

internal sealed class ForestDuelHud
{
    private readonly Texture2D kaiching = Resources.Load<Texture2D>("Firelight/DuelHud/KaichingPortrait");
    private readonly Texture2D wangHao = Resources.Load<Texture2D>("Firelight/DuelHud/WangHaoPortrait");
    private readonly Texture2D kaichingName = Resources.Load<Texture2D>("Firelight/Kaiching");
    private readonly Texture2D wangHaoName = Resources.Load<Texture2D>("Firelight/Wanghao");
    private readonly Texture2D rubyMouseHint = Resources.Load<Texture2D>("Firelight/DuelHud/RubyMouseHint");
    private readonly Texture2D rubyTouchHint = Resources.Load<Texture2D>("Firelight/DuelHud/RubyTouchHint");
    private static readonly Color ink = new(.04f, .055f, .075f, .94f);
    private static readonly Color empty = new(.18f, .22f, .25f);
    private static readonly Color ember = new(.96f, .42f, .22f);
    private static readonly Color ice = new(.28f, .64f, 1f);
    private static readonly Color green = new(.33f, .86f, .57f);
    private GUIStyle healthText;
    private GUIStyle detailText;

    public void Draw(int playerHealth, int playerMaximum, HunterAI boss, string encounter, bool touch, string feedback)
    {
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 990f, Screen.height / 660f), .62f, 1.5f);
        float inset = 16f * scale;
        float width = Mathf.Min(235f * scale, Screen.width * .34f);
        float portraitSize = Mathf.Min(width, 94f * scale);
        float nameHeight = 25f * scale;
        float barHeight = 22f * scale;

        healthText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        detailText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        healthText.fontSize = Mathf.RoundToInt(14f * scale);
        detailText.fontSize = Mathf.Max(10, Mathf.RoundToInt(15f * scale));

        Color original = GUI.color;
        DrawFighter(inset, inset, width, portraitSize, nameHeight, barHeight, false,
            kaiching, kaichingName, "主人公", playerHealth, playerMaximum, ember);
        if (boss != null)
            DrawFighter(Screen.width - inset - width, inset, width, portraitSize, nameHeight, barHeight, true,
                wangHao, wangHaoName, "魔王", boss.Health, boss.MaxHealth, boss.IsReceivingHealing ? green : ice);

        float centerWidth = Mathf.Min(260f * scale, Screen.width - 2f * (inset + width + 4f * scale));
        if (centerWidth > 0f)
        {
            Rect status = new((Screen.width - centerWidth) * .5f, inset + 4f * scale, centerWidth, 40f * scale);
            DrawDetail(status, encounter);
            if (feedback != null)
            {
                status.y += status.height + 5f * scale;
                DrawDetail(status, feedback);
            }
        }

        float guideWidth = Mathf.Min(Screen.width - 2f * inset, 410f * scale);
        Rect credits = new(inset, Screen.height - inset - 40f * scale, guideWidth, 40f * scale);
        DrawDetail(credits, "Legionnaire by Scott Buckley (CC BY 4.0) · www.scottbuckley.com.au");
        credits.y -= 42f * scale;
        DrawDetail(credits, touch ? "Tap ground to move · Tap enemy to attack" :
            "Arrows: move · Left click: attack · Right click: heal");
        GUI.color = original;
    }

    public void DrawRubyHint(Vector3 screen, bool touch)
    {
        if (screen.z <= 0f || !new Rect(0f, 0f, Screen.width, Screen.height).Contains(screen))
            return;

        float scale = Mathf.Clamp(Screen.height / 720f, .78f, 1.35f);
        float width = Mathf.Min(300f * scale, Screen.width - 16f);
        float height = 54f * scale;
        float x = Mathf.Clamp(screen.x - width * .5f, 8f, Screen.width - width - 8f);
        float y = Screen.height - screen.y;
        float tooltipY = Mathf.Min(y + 16f * scale, Screen.height - height - 8f);
        var box = new Rect(x, tooltipY, width, height);
        Color original = GUI.color;
        Fill(box, ink);
        Fill(new Rect(box.x, box.y, 3f, box.height), ember);
        GUI.color = Color.white;
        Texture2D texture = touch ? rubyTouchHint : rubyMouseHint;
        if (texture != null)
            GUI.DrawTexture(new Rect(x + 8f, tooltipY + 4f, width - 16f, height - 8f), texture, ScaleMode.ScaleToFit);
        else
            GUI.Label(box, touch ? "Tap next to the ruby to recover 5 HP." :
                "Walk over the ruby for 5 HP. Right-click to heal.", detailText);
        GUI.color = original;
    }

    private void DrawFighter(float x, float y, float width, float portraitSize, float nameHeight, float barHeight,
        bool right, Texture2D portrait, Texture2D name, string fallbackName, int hp, int maximum, Color accent)
    {
        const float border = 2f;
        float outerX = right ? x + width - portraitSize : x;
        Rect photo = new(outerX, y, portraitSize, portraitSize);
        Fill(photo, accent);
        Rect inner = new(photo.x + border, photo.y + border, photo.width - 2f * border, photo.height - 2f * border);
        Fill(inner, ink);
        GUI.color = Color.white;
        if (portrait != null)
            GUI.DrawTexture(inner, portrait, ScaleMode.ScaleAndCrop);

        Rect caption = new(outerX, photo.yMax + border, portraitSize, nameHeight);
        Fill(caption, ink);
        GUI.color = Color.white;
        if (name != null)
            GUI.DrawTexture(caption, name, ScaleMode.ScaleToFit);
        else
            GUI.Label(caption, fallbackName, healthText);

        Rect bar = new(x, caption.yMax + border, width, barHeight);
        Fill(bar, ink);
        Rect track = new(x + border, bar.y + border, width - 2f * border, bar.height - 2f * border);
        Fill(track, empty);
        float filled = track.width * Mathf.Clamp01(hp / (float)Mathf.Max(1, maximum));
        if (filled > 0f)
            Fill(new Rect(right ? track.xMax - filled : track.x, track.y, filled, track.height), accent);
        GUI.color = Color.white;
        GUI.Label(bar, $"{hp} / {maximum}", healthText);
    }

    private void DrawDetail(Rect box, string text)
    {
        Fill(box, ink);
        GUI.color = Color.white;
        GUI.Label(box, text, detailText);
    }

    private static void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }
}
