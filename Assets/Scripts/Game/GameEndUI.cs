using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using AuraKitchen;

public class GameEndUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI plateServedText;
    [SerializeField] private TextMeshProUGUI ingrUtilRateText;
    [SerializeField] private Button playAgainButton;

    [SerializeField] private string plateServedTextFormat;
    [SerializeField] private string ingrUtilRateTextFormat;

    private GameObject modernModalObj;
    private TextMeshProUGUI modernStarsText;
    private TextMeshProUGUI modernRankTitleText;
    private TextMeshProUGUI modernPlatesValText;
    private TextMeshProUGUI modernScoreValText;
    private TextMeshProUGUI modernStreakValText;
    private TextMeshProUGUI modernEfficiencyValText;

    private void Awake()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(PlayAgainAction);
        }
    }

    private void Start()
    {
        GameManager.Instance.OnGameStateChanged += GameManager_OnGameStateChanged;
        BuildModernShiftSummaryModal();
        HideEndUI();
    }

    private void PlayAgainAction()
    {
        SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
    }

    private void MainMenuAction()
    {
        SceneSwitcher.LoadScene(SceneSwitcher.Scene.MainMenu);
    }

    private void GameManager_OnGameStateChanged(object sender, GameManager.GameStateChangedArg e)
    {
        if (e.newState == GameManager.State.GAME_END)
        {
            ShowEndUI();
        }
        else
        {
            HideEndUI();
        }
    }

    private void ShowEndUI()
    {
        // Hide legacy tutorial children
        foreach (Transform child in transform)
        {
            if (child.gameObject != modernModalObj)
            {
                child.gameObject.SetActive(false);
            }
        }

        if (modernModalObj != null)
        {
            modernModalObj.SetActive(true);
            PopulateModernStats();
        }
    }

    private void HideEndUI()
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(false);
        }
    }

    private void PopulateModernStats()
    {
        int plates = GameManager.Instance != null ? GameManager.Instance.PlateServed : 0;
        int score = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.CurrentScore : plates * 150;
        int maxStreak = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.MaxComboStreak : 1;
        float efficiency = GameManager.Instance != null ? GameManager.Instance.IngredientUtilityPercent : 100f;

        // Stars & Title
        string stars;
        string rankTitle;

        if (AuraOnlineMultiplayerManager.Instance != null && AuraOnlineMultiplayerManager.Instance.IsOnlineActive)
        {
            stars = "<color=#ffd700>★ ★ ★</color>";
            rankTitle = AuraOnlineMultiplayerManager.Instance.GetChallengeWinnerSummary();
        }
        else if (plates >= 5)
        {
            stars = "<color=#ffd700>★ ★ ★</color>";
            rankTitle = "<color=#ffd700>👑 MASTER OF AURA (3-STAR CHEF)</color>";
        }
        else if (plates >= 3)
        {
            stars = "<color=#ffd700>★ ★</color><color=#555555> ☆</color>";
            rankTitle = "<color=#00e5ff>⭐ EXECUTIVE SOUS-CHEF</color>";
        }
        else if (plates >= 1)
        {
            stars = "<color=#ffd700>★</color><color=#555555> ☆ ☆</color>";
            rankTitle = "<color=#ffaa00>🍳 CERTIFIED LINE COOK</color>";
        }
        else
        {
            stars = "<color=#555555>☆ ☆ ☆</color>";
            rankTitle = "<color=#ff6666>KITCHEN APPRENTICE (Keep practicing!)</color>";
        }

        if (modernStarsText != null) modernStarsText.text = stars;
        if (modernRankTitleText != null) modernRankTitleText.text = rankTitle;
        if (modernPlatesValText != null) modernPlatesValText.text = $"{plates} Dishes";
        if (modernScoreValText != null) modernScoreValText.text = $"{score:N0} PTS";
        if (modernStreakValText != null) modernStreakValText.text = $"x{Mathf.Max(1, maxStreak)} Max";
        if (modernEfficiencyValText != null) modernEfficiencyValText.text = $"{efficiency:0}% Precision";
    }

    private void BuildModernShiftSummaryModal()
    {
        modernModalObj = new GameObject("Aura_Modern_ShiftSummary");
        modernModalObj.transform.SetParent(transform, false);

        RectTransform rootRt = modernModalObj.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        // 1. Dark Backdrop
        Image bgImg = modernModalObj.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.88f);

        // 2. Center Card (High-res charcoal)
        GameObject card = new GameObject("Card_Container");
        card.transform.SetParent(modernModalObj.transform, false);
        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(1040, 720);

        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.18f, 0.18f, 0.18f, 0.98f);

        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.40f, 0.40f, 0.40f, 1f);
        cardOutline.effectDistance = new Vector2(3, -3);

        // Title
        CreateText(card, "AURA KITCHEN - SHIFT COMPLETED!", 32, FontStyles.Bold,
            new Vector2(0, 310), new Vector2(900, 48), Color.white);

        CreateText(card, "Restaurant Performance & Ratings", 18, FontStyles.Normal,
            new Vector2(0, 270), new Vector2(800, 30), new Color(0.75f, 0.75f, 0.75f));

        // 3 Big Gold Stars
        modernStarsText = CreateText(card, "★ ★ ★", 54, FontStyles.Bold,
            new Vector2(0, 205), new Vector2(600, 70), new Color(1.0f, 0.85f, 0.2f));

        // Chef Rank Badge
        modernRankTitleText = CreateText(card, "👑 MASTER OF AURA", 24, FontStyles.Bold,
            new Vector2(0, 140), new Vector2(800, 40), Color.white);

        // 4 Stat Cards
        CreateStatTile(card, "DISHES SERVED", "0 Dishes", new Vector2(-250, 45), out modernPlatesValText);
        CreateStatTile(card, "TOTAL AURA SCORE", "0 PTS", new Vector2(250, 45), out modernScoreValText);
        CreateStatTile(card, "MAX COMBO STREAK", "x1", new Vector2(-250, -85), out modernStreakValText);
        CreateStatTile(card, "KITCHEN EFFICIENCY", "100%", new Vector2(250, -85), out modernEfficiencyValText);

        // Buttons
        CreateButton(card, "PLAY AGAIN", new Vector2(-220, -250), new Vector2(400, 80),
            new Color(0.18f, 0.58f, 0.28f), PlayAgainAction, 24);

        CreateButton(card, "MAIN MENU", new Vector2(220, -250), new Vector2(400, 80),
            new Color(0.28f, 0.28f, 0.28f), MainMenuAction, 24);

        modernModalObj.SetActive(false);
    }

    private void CreateStatTile(GameObject parent, string title, string defaultVal, Vector2 pos, out TextMeshProUGUI valText)
    {
        GameObject tile = new GameObject("Tile_" + title);
        tile.transform.SetParent(parent.transform, false);
        RectTransform rt = tile.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(460, 100);

        Image img = tile.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        Outline outline = tile.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.28f, 0.28f, 0.8f);

        CreateText(tile, title, 16, FontStyles.Bold, new Vector2(0, 22), new Vector2(440, 28), new Color(0.70f, 0.70f, 0.70f));
        valText = CreateText(tile, defaultVal, 28, FontStyles.Bold, new Vector2(0, -16), new Vector2(440, 42), Color.white);
    }

    private TextMeshProUGUI CreateText(GameObject parent, string text, float fontSize, FontStyles style, Vector2 pos, Vector2 size, Color color)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent.transform, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void CreateButton(GameObject parent, string label, Vector2 pos, Vector2 size, Color bgColor, Action onClick, float fontSize)
    {
        GameObject btnObj = new GameObject("Btn_" + label);
        btnObj.transform.SetParent(parent.transform, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform trt = txtObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
    }
}
