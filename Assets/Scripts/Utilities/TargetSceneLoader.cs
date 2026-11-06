using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class TargetSceneLoader : MonoBehaviour
{
    private TextMeshProUGUI loadingText;

    private void Start()
    {
        SetupLoadingVisuals();
        StartCoroutine(LoadDestScene());
    }

    private void SetupLoadingVisuals()
    {
        loadingText = FindObjectOfType<TextMeshProUGUI>();
        if (loadingText != null)
        {
            // Apply official game font asset & material
            TMP_FontAsset gameFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (gameFont != null)
            {
                loadingText.font = gameFont;
                loadingText.fontSharedMaterial = gameFont.material;
            }

            loadingText.fontSize = 44;
            loadingText.fontStyle = FontStyles.Bold;
            loadingText.color = new Color(0.95f, 0.98f, 1f);
            loadingText.alignment = TextAlignmentOptions.Center;

            // Center on screen
            RectTransform rt = loadingText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(800, 100);
        }
    }

    private IEnumerator LoadDestScene()
    {
        int dots = 0;
        AsyncOperation op = SceneManager.LoadSceneAsync(SceneSwitcher.TargetScene.ToString());
        op.allowSceneActivation = false;

        float minWait = 0.5f;
        float elapsed = 0f;

        while (elapsed < minWait || op.progress < 0.9f)
        {
            elapsed += Time.deltaTime;
            dots = (dots + 1) % 4;
            if (loadingText != null)
            {
                loadingText.text = $"LOADING KITCHEN{new string('.', dots)}";
            }
            yield return new WaitForSeconds(0.12f);
        }

        if (loadingText != null)
        {
            loadingText.text = "READY!";
        }
        yield return new WaitForSeconds(0.1f);

        op.allowSceneActivation = true;
    }
}
