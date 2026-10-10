using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PLACEHOLDER race buff buttons (plain coloured squares), built in code under the race HUD. One buff per race: after one
/// is pressed all four stop working, and the pressed one shows its time left. Replace with the real art later.
/// </summary>
public class BuffBar : MonoBehaviour
{
    private struct Def
    {
        public BuffType type;
        public string label;
        public Color color;

        public Def(BuffType type, string label, Color color)
        {
            this.type = type;
            this.label = label;
            this.color = color;
        }
    }

    private static readonly Def[] Defs =
    {
        new Def(BuffType.Speed, "SPEED", new Color(1f, 0.55f, 0.1f)),
        new Def(BuffType.Giant, "GIANT", new Color(0.45f, 0.8f, 0.2f)),
        new Def(BuffType.Freeze, "FREEZE", new Color(0.3f, 0.75f, 1f)),
        new Def(BuffType.DoubleRewards, "2X\nREWARDS", new Color(1f, 0.8f, 0.1f)),
    };

    private const float ButtonSize = 74f;
    private const float Spacing = 84f;
    private const float BottomY = 120f;
    private static readonly Color UsedUpColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);

    private Image[] backs;
    private RectTransform[] timers;
    private TMP_Text[] labels;
    private Button[] buttons;

    /// <summary>Builds the bar under the HUD.</summary>
    public static BuffBar Create(RectTransform parent, TMP_FontAsset font)
    {
        RectTransform root = NewRect("BuffBar (placeholder)", parent);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(Spacing * Defs.Length, ButtonSize);
        root.anchoredPosition = new Vector2(0f, BottomY);

        BuffBar bar = root.gameObject.AddComponent<BuffBar>();
        bar.Build(font);
        return bar;
    }

    private void Build(TMP_FontAsset font)
    {
        int n = Defs.Length;
        backs = new Image[n];
        timers = new RectTransform[n];
        labels = new TMP_Text[n];
        buttons = new Button[n];

        for (int i = 0; i < n; i++)
        {
            Def def = Defs[i];
            RectTransform button = NewRect(def.type + " Buff", (RectTransform)transform);
            button.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            button.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * Spacing, 0f);
            backs[i] = button.gameObject.AddComponent<Image>();
            buttons[i] = button.gameObject.AddComponent<Button>();
            buttons[i].transition = Selectable.Transition.None;   // the colours are set in Refresh
            buttons[i].onClick.AddListener(() => Press(def.type));

            // Dark overlay that drains from the top while the buff runs.
            RectTransform timer = NewRect("Timer", button);
            timer.anchorMin = Vector2.zero;
            timer.anchorMax = Vector2.one;
            timer.sizeDelta = Vector2.zero;
            Image shade = timer.gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.35f);
            shade.raycastTarget = false;
            timers[i] = timer;

            RectTransform labelRect = NewRect("Label", button);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = new Vector2(-6f, -6f);
            TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 20f;
            label.color = Color.white;
            label.raycastTarget = false;
            labels[i] = label;
        }

        Refresh();
    }

    private void Press(BuffType type)
    {
        if (RaceBuffs.Instance.TryUse(type))
        {
            AudioManager.Play(Sfx.Click);
            Refresh();
        }
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        RaceBuffs buffs = RaceBuffs.Instance;
        bool canUse = buffs.CanUse;

        for (int i = 0; i < Defs.Length; i++)
        {
            Def def = Defs[i];
            bool used = buffs.Used == def.type;
            bool running = buffs.Active == def.type;

            buttons[i].interactable = canUse;
            backs[i].color = canUse || running ? def.color : UsedUpColor;

            float left = running && def.type != BuffType.DoubleRewards ? Mathf.Clamp01(buffs.TimeLeft / Mathf.Max(0.01f, buffs.TotalTime)) : 0f;
            timers[i].gameObject.SetActive(left > 0f);
            timers[i].anchorMin = new Vector2(0f, 1f - left);

            string text = def.label;
            if (running && def.type == BuffType.DoubleRewards)
            {
                text = "2X\nON";
            }
            else if (running)
            {
                text = buffs.TimeLeft.ToString("0.0") + "s";
            }
            else if (used)
            {
                text = "USED";
            }

            if (labels[i].text != text)
            {
                labels[i].text = text;
            }
        }
    }

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        return rect;
    }
}
