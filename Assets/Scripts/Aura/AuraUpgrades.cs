using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    /// <summary>Persistent meta-progress: coins, shift counter and chef upgrades (PlayerPrefs).</summary>
    public static class AuraProgress
    {
        public static int Coins
        {
            get { return PlayerPrefs.GetInt("Aura_Coins", 0); }
            set { PlayerPrefs.SetInt("Aura_Coins", Mathf.Max(0, value)); }
        }

        public static int Shift
        {
            get { return Mathf.Max(1, PlayerPrefs.GetInt("Aura_Shift", 1)); }
            set { PlayerPrefs.SetInt("Aura_Shift", Mathf.Max(1, value)); }
        }
    }

    public static class AuraUpgrades
    {
        public struct Def
        {
            public string id, name, desc;
            public int max;
            public AuraIcon icon;
        }

        public static readonly Def[] All =
        {
            new Def { id = "speed", name = "Quick Boots", desc = "+6% chef speed per level", max = 5, icon = AuraIcon.Flame },
            new Def { id = "time",  name = "Extra Hour",  desc = "+4s starting time per level", max = 5, icon = AuraIcon.Clock },
            new Def { id = "tips",  name = "Tip Jar",     desc = "+25% tips per level", max = 5, icon = AuraIcon.Coin },
        };

        private static float cachedSpeed = -1f;

        private static string Key(string id) { return "Aura_Up_" + id; }
        public static int Level(string id) { return PlayerPrefs.GetInt(Key(id), 0); }
        public static int Cost(string id) { return 60 + Level(id) * 55; }

        public static float SpeedMult
        {
            get
            {
                if (cachedSpeed < 0f) cachedSpeed = 1f + 0.06f * Level("speed");
                return cachedSpeed;
            }
        }
        public static float ExtraTime { get { return 4f * Level("time"); } }
        public static float TipMult { get { return 1f + 0.25f * Level("tips"); } }

        public static bool TryBuy(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].id != id) continue;
                int lvl = Level(id);
                if (lvl >= All[i].max) return false;
                int cost = Cost(id);
                if (AuraProgress.Coins < cost) return false;
                AuraProgress.Coins -= cost;
                PlayerPrefs.SetInt(Key(id), lvl + 1);
                PlayerPrefs.Save();
                cachedSpeed = -1f;
                return true;
            }
            return false;
        }
    }

    /// <summary>Between-shift upgrade shop, built entirely from the Aura kit.</summary>
    public class AuraUpgradeShop : MonoBehaviour
    {
        private static AuraUpgradeShop instance;

        private TextMeshProUGUI coinsText;
        private readonly List<Row> rows = new List<Row>();

        private class Row
        {
            public string id;
            public Image[] pips;
            public Button buy;
            public TextMeshProUGUI buyLabel;
        }

        public static void Open()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Aura_UpgradeShop");
                instance = go.AddComponent<AuraUpgradeShop>();
                instance.Build();
            }
            instance.gameObject.SetActive(true);
            instance.Refresh();
        }

        public static void Close()
        {
            if (instance != null) instance.gameObject.SetActive(false);
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.AddComponent<AuraSkinned>();

            RectTransform scrim = AuraTheme.MakeRect(transform, "Scrim");
            scrim.anchorMin = Vector2.zero;
            scrim.anchorMax = Vector2.one;
            scrim.offsetMin = scrim.offsetMax = Vector2.zero;
            Image si = scrim.gameObject.AddComponent<Image>();
            si.color = AuraTheme.Scrim;

            GameObject board = AuraTheme.CreatePanel(transform, "Board", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 720), AuraTheme.PanelFill, AuraTheme.Stroke);
            board.AddComponent<AuraPopIn>();

            AuraTheme.CreateText(board.transform, "UPGRADES", 56, FontStyles.Normal, new Vector2(-250, 300), new Vector2(480, 80), AuraTheme.Gold);

            GameObject coinPill = AuraTheme.CreatePanel(board.transform, "CoinPill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(300, 300), new Vector2(260, 64), AuraTheme.PanelDeep, AuraTheme.Stroke, false);
            AuraTheme.CreateIcon(coinPill.transform, AuraIcons.Get(AuraIcon.Coin), new Vector2(-90, 0), 40f, AuraTheme.Gold);
            coinsText = AuraTheme.CreateText(coinPill.transform, "0", 34, FontStyles.Normal, new Vector2(20, 0), new Vector2(160, 50), AuraTheme.TextPrimary);

            for (int i = 0; i < AuraUpgrades.All.Length; i++)
                BuildRow(board.transform, AuraUpgrades.All[i], new Vector2(0, 150 - i * 140));

            AuraTheme.CreateButton(board.transform, "Btn_Close", "Done", new Vector2(0, -300), new Vector2(320, 76),
                AuraTheme.ButtonStyle.Danger, Close, 32, AuraIcons.Get(AuraIcon.Check));
        }

        private void BuildRow(Transform parent, AuraUpgrades.Def def, Vector2 pos)
        {
            GameObject row = AuraTheme.CreatePanel(parent, "Row_" + def.id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), pos, new Vector2(880, 116), AuraTheme.PanelRaised, AuraTheme.StrokeSoft, false);

            AuraTheme.CreateIcon(row.transform, AuraIcons.Get(def.icon), new Vector2(-380, 0), 60f, AuraTheme.Gold);
            AuraTheme.CreateText(row.transform, def.name, 30, FontStyles.Normal, new Vector2(-90, 24), new Vector2(460, 40), AuraTheme.TextPrimary, TextAlignmentOptions.Left);
            AuraTheme.CreateText(row.transform, def.desc, 19, FontStyles.Normal, new Vector2(-90, -18), new Vector2(460, 30), AuraTheme.TextSecondary, TextAlignmentOptions.Left);

            Row r = new Row { id = def.id, pips = new Image[def.max] };
            for (int i = 0; i < def.max; i++)
            {
                RectTransform p = AuraTheme.MakeRect(row.transform, "Pip");
                p.anchoredPosition = new Vector2(100 + i * 28, 0);
                p.sizeDelta = new Vector2(20, 20);
                Image pi = p.gameObject.AddComponent<Image>();
                pi.sprite = AuraTheme.Dot;
                pi.raycastTarget = false;
                r.pips[i] = pi;
            }

            string id = def.id;
            GameObject btn = AuraTheme.CreateButton(row.transform, "Btn_Buy", "BUY", new Vector2(320, 0), new Vector2(210, 72),
                AuraTheme.ButtonStyle.Success, () => { if (AuraUpgrades.TryBuy(id)) Refresh(); }, 28);
            r.buy = btn.GetComponent<Button>();
            r.buyLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            rows.Add(r);
        }

        private void Refresh()
        {
            if (coinsText != null) coinsText.text = AuraProgress.Coins.ToString("N0");
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                int lvl = AuraUpgrades.Level(r.id);
                for (int p = 0; p < r.pips.Length; p++)
                    r.pips[p].color = p < lvl ? AuraTheme.Gold : new Color(0f, 0f, 0f, 0.35f);

                bool maxed = lvl >= r.pips.Length;
                int cost = AuraUpgrades.Cost(r.id);
                r.buy.interactable = !maxed && AuraProgress.Coins >= cost;
                if (r.buyLabel != null) r.buyLabel.text = maxed ? "MAX" : "BUY " + cost;
            }
        }
    }
}
