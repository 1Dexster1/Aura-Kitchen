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
    private TextMeshProUGUI modernCoinsText;
    private Coroutine showRoutine;

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
        if (AuraKitchen.AuraOnlineMultiplayerManager.Instance != null && AuraKitchen.AuraOnlineMultiplayerManager.Instance.IsOnlineActive)
        {
            AuraKitchen.AuraOnlineMultiplayerManager.Instance.ReturnToLobby();
        }
        else
        {
            SceneSwitcher.LoadScene(SceneSwitcher.Scene.GameScene);
        }
    }

    private void MainMenuAction()
    {
        SceneSwitcher.LoadScene(SceneSwitcher.Scene.MainMenu);
    }

    private void GameManager_OnGameStateChanged(object sender, GameManager.GameStateChangedArg e)
    {
        if (e.newState == GameManager.State.GAME_END)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(ShowEndUIDelayed());
        }
        else
        {
            if (showRoutine != null) { StopCoroutine(showRoutine); showRoutine = null; }
            HideEndUI();
        }
    }

    private void RefreshCoinsLine()
    {
        if (modernCoinsText == null) return;
        int earned = AuraShowtime.Instance != null ? AuraShowtime.Instance.LastShiftCoins : 0;
        modernCoinsText.text = "+" + earned + " coins this shift   |   Wallet: " + AuraProgress.Coins.ToString("N0");
    }

    private IEnumerator ShowEndUIDelayed()
    {
        // let the cinematic "TIME'S UP" beat play before the results card pops in
        float delay = AuraShowtime.Instance != null ? AuraShowtime.Instance.OutroDelay : 0f;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        ShowEndUI();
        showRoutine = null;
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
            RefreshCoinsLine();
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

        // Rating & Rank Title
        string ratingText;
        string rankTitle;

        if (AuraOnlineMultiplayerManager.Instance != null && AuraOnlineMultiplayerManager.Instance.IsOnlineActive)
        {
            ratingText = "<color=" + AuraTheme.Hex(AuraTheme.GoldText) + ">MULTIPLAYER CLASH COMPLETED</color>";
            rankTitle = AuraOnlineMultiplayerManager.Instance.GetChallengeWinnerSummary();
        }
        else if (plates >= 5)
        {
            ratingText = "<color=" + AuraTheme.Hex(AuraTheme.GoldText) + ">PERFORMANCE: 3-STAR EXCELLENCE</color>";
            rankTitle = "<color=" + AuraTheme.Hex(AuraTheme.GoldText) + ">RANK: MASTER OF AURA</color>";
        }
        else if (plates >= 3)
        {
            ratingText = "<color=" + AuraTheme.Hex(AuraTheme.GoldText) + ">PERFORMANCE: 2-STAR GREAT</color>";
            rankTitle = "<color=" + AuraTheme.Hex(AuraTheme.Cyan) + ">RANK: EXECUTIVE SOUS-CHEF</color>";
        }
        else if (plates >= 1)
        {
            ratingText = "<color=" + AuraTheme.Hex(AuraTheme.AmberText) + ">PERFORMANCE: 1-STAR PASSED</color>";
            rankTitle = "<color=" + AuraTheme.Hex(AuraTheme.AmberText) + ">RANK: CERTIFIED LINE COOK</color>";
        }
        else
        {
            ratingText = "<color=" + AuraTheme.Hex(AuraTheme.TextSecondary) + ">PERFORMANCE: UNRANKED</color>";
            rankTitle = "<color=" + AuraTheme.Hex(AuraTheme.Danger) + ">RANK: KITCHEN APPRENTICE</color>";
        }

        if (modernStarsText != null) modernStarsText.text = ratingText;
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
        bgImg.color = AuraTheme.Scrim;

        // 2. Center glass card
        GameObject card = AuraTheme.CreatePanel(modernModalObj.transform, "Card_Container",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(1040, 720), AuraTheme.PanelFill, AuraTheme.Stroke);

        // Title
        CreateText(card, "<color=" + AuraTheme.Hex(AuraTheme.Cyan) + ">AURA</color> <color=" + AuraTheme.Hex(AuraTheme.Danger) + ">KITCHEN</color>  -  SHIFT COMPLETED", 32, FontStyles.Bold,
            new Vector2(0, 310), new Vector2(900, 48), AuraTheme.TextPrimary);

        CreateText(card, "Restaurant Performance & Ratings", 18, FontStyles.Normal,
            new Vector2(0, 270), new Vector2(800, 30), AuraTheme.TextSecondary);

        // Performance Rating & Chef Rank
        modernStarsText = CreateText(card, "PERFORMANCE: 3-STAR EXCELLENCE", 26, FontStyles.Bold,
            new Vector2(0, 205), new Vector2(800, 50), AuraTheme.Gold);

        modernRankTitleText = CreateText(card, "RANK: MASTER OF AURA", 24, FontStyles.Bold,
            new Vector2(0, 150), new Vector2(800, 40), AuraTheme.TextPrimary);

        // 4 Stat Cards
        CreateStatTile(card, "DISHES SERVED", "0 Dishes", new Vector2(-250, 45), AuraIcon.Cloche, AuraTheme.Cyan, out modernPlatesValText);
        CreateStatTile(card, "TOTAL AURA SCORE", "0 PTS", new Vector2(250, 45), AuraIcon.Star, AuraTheme.GoldText, out modernScoreValText);
        CreateStatTile(card, "MAX COMBO STREAK", "x1", new Vector2(-250, -85), AuraIcon.Flame, AuraTheme.Ember, out modernStreakValText);
        CreateStatTile(card, "KITCHEN EFFICIENCY", "100%", new Vector2(250, -85), AuraIcon.Check, AuraTheme.Success, out modernEfficiencyValText);

        // Coins earned this shift + wallet
        modernCoinsText = CreateText(card, "", 26, FontStyles.Normal, new Vector2(0, -172), new Vector2(900, 40), AuraTheme.TextPrimary);

        // Buttons
        AuraTheme.CreateButton(card.transform, "Btn_PlayAgain", "Next Shift", new Vector2(-335, -258), new Vector2(300, 80),
            AuraTheme.ButtonStyle.Success, PlayAgainAction, 28, AuraIcons.Get(AuraIcon.Check));

        AuraTheme.CreateButton(card.transform, "Btn_Upgrades", "Upgrades", new Vector2(0, -258), new Vector2(300, 80),
            AuraTheme.ButtonStyle.Gold, () => { AuraUpgradeShop.Open(); RefreshCoinsLine(); }, 28, AuraIcons.Get(AuraIcon.Coin));

        AuraTheme.CreateButton(card.transform, "Btn_MainMenu", "Main Menu", new Vector2(335, -258), new Vector2(300, 80),
            AuraTheme.ButtonStyle.Primary, MainMenuAction, 28, AuraIcons.Get(AuraIcon.Plate));

        modernModalObj.SetActive(false);
    }

    private void CreateStatTile(GameObject parent, string title, string defaultVal, Vector2 pos, AuraIcon icon, Color iconColor, out TextMeshProUGUI valText)
    {
        GameObject tile = AuraTheme.CreatePanel(parent.transform, "Tile_" + title,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            pos, new Vector2(460, 100), AuraTheme.PanelRaised, AuraTheme.StrokeSoft, false);

        AuraTheme.CreateIcon(tile.transform, AuraIcons.Get(icon), new Vector2(-190, 0), 48f, iconColor);

        CreateText(tile, title, 16, FontStyles.Bold, new Vector2(40, 22), new Vector2(340, 28), AuraTheme.TextSecondary);
        valText = CreateText(tile, defaultVal, 28, FontStyles.Bold, new Vector2(40, -16), new Vector2(340, 42), AuraTheme.TextPrimary);
    }

    private TextMeshProUGUI CreateText(GameObject parent, string text, float fontSize, FontStyles style, Vector2 pos, Vector2 size, Color color)
    {
        return AuraTheme.CreateText(parent.transform, text, fontSize, style, pos, size, color);
    }
}
