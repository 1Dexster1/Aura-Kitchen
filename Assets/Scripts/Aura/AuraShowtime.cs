using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AuraKitchen
{
    /// <summary>
    /// The "don't be boring" layer: cinematic intro/outro camera with letterbox and title cards,
    /// random shift events (Rush Hour, Sugar Rush, VIP Guest), customer ratings with tips,
    /// hit-stop + flash on deliveries, and coin/shift progression that feeds the upgrade shop.
    /// Everything is built from code; nothing in the scene has to change.
    /// </summary>
    public class AuraShowtime : MonoBehaviour
    {
        public static AuraShowtime Instance { get; private set; }

        // ---- modifiers read by other systems (reset every shift) ----
        public static float SpeedBonus = 1f;
        public static int ScoreBonusMult = 1;
        private static float eventSpawn = 1f;
        public static float SpawnRateMult
        {
            get
            {
                // each finished shift makes the lunch crowd a little hungrier (capped)
                float ramp = 1f + 0.06f * Mathf.Min(AuraProgress.Shift - 1, 8);
                return ramp * eventSpawn;
            }
        }

        public float OutroDelay { get; private set; }
        public int LastShiftCoins { get; private set; }

        private static readonly string[] Tips =
        {
            "Chain 4 deliveries to unlock AURA RUSH",
            "Fast service earns 3-star reviews and bigger tips",
            "Every dish you serve adds bonus time",
            "VIP guests pay big, don't keep them waiting",
            "Spend your coins on upgrades between shifts",
        };

        // ---- ui ----
        private Canvas canvas;
        private RectTransform barTop, barBottom;
        private float barTarget, barCurrent;
        private CanvasGroup titleGroup;
        private TextMeshProUGUI titleText, titleSub;
        private RectTransform titleRect;
        private Image flash;

        private GameObject eventPanel;
        private TextMeshProUGUI eventTitle, eventSub;
        private Image eventBar, eventIcon;
        private float eventRemaining, eventTotal;

        // ---- state ----
        private Camera cine;
        private Coroutine introCo, outroCo, eventCo;
        private bool ended;
        private bool online;
        private bool vipActive;
        private int shiftCoins;
        private readonly List<KeyValuePair<MenuRecipeSO, float>> placed = new List<KeyValuePair<MenuRecipeSO, float>>();

        // ------------------------------------------------------------------ lifecycle
        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            ResetModifiers();
        }

        private void Start()
        {
            BuildUI();
            if (GameManager.Instance != null) GameManager.Instance.OnGameStateChanged += OnState;
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderPlaced += OnPlaced;
                DeliveryManager.Instance.OnOrderDelivered += OnDelivered;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnGameStateChanged -= OnState;
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.OnOrderPlaced -= OnPlaced;
                DeliveryManager.Instance.OnOrderDelivered -= OnDelivered;
            }
            ResetModifiers();
            if (cine != null) Destroy(cine.gameObject);
            if (canvas != null) Destroy(canvas.gameObject);
            if (Instance == this) Instance = null;
        }

        private static void ResetModifiers()
        {
            SpeedBonus = 1f;
            ScoreBonusMult = 1;
            eventSpawn = 1f;
        }

        private void Update()
        {
            // letterbox bars glide in/out regardless of timeScale
            barCurrent = Mathf.MoveTowards(barCurrent, barTarget, 260f * Time.unscaledDeltaTime);
            if (barTop != null) barTop.sizeDelta = new Vector2(0f, barCurrent);
            if (barBottom != null) barBottom.sizeDelta = new Vector2(0f, barCurrent);

            if (eventPanel != null && eventPanel.activeSelf && eventTotal > 0f && eventBar != null)
            {
                eventBar.fillAmount = Mathf.Clamp01(eventRemaining / eventTotal);
            }
        }

        // ------------------------------------------------------------------ state machine hooks
        private void OnState(object sender, GameManager.GameStateChangedArg e)
        {
            if (e.newState == GameManager.State.GAME_START_COUNTDOWN && e.lastState != GameManager.State.GAME_PAUSING)
            {
                ended = false;
                placed.Clear();
                ResetModifiers();
                StopAllCoroutinesSafe();
                introCo = StartCoroutine(IntroRoutine());
            }
            else if (e.newState == GameManager.State.GAME_RUNNING && e.lastState == GameManager.State.GAME_START_COUNTDOWN)
            {
                FinishIntro();
                shiftCoins = 0;
                if (GameManager.Instance != null) GameManager.Instance.AddBonusTime(AuraUpgrades.ExtraTime);
                online = AuraOnlineMultiplayerManager.Instance != null && AuraOnlineMultiplayerManager.Instance.IsOnlineActive;
                if (!online) eventCo = StartCoroutine(EventLoop());
            }
            else if (e.newState == GameManager.State.GAME_END)
            {
                ended = true;
                ResetModifiers();
                HideEvent();
                if (eventCo != null) StopCoroutine(eventCo);
                AwardCoins();
                OutroDelay = 2.1f;
                outroCo = StartCoroutine(OutroRoutine());
            }
        }

        private void StopAllCoroutinesSafe()
        {
            if (introCo != null) StopCoroutine(introCo);
            if (outroCo != null) StopCoroutine(outroCo);
            if (eventCo != null) StopCoroutine(eventCo);
            introCo = outroCo = eventCo = null;
        }

        private void AwardCoins()
        {
            int score = AuraGameMechanics.Instance != null ? AuraGameMechanics.Instance.CurrentScore : 0;
            LastShiftCoins = shiftCoins + score / 25;
            AuraProgress.Coins += LastShiftCoins;
            AuraProgress.Shift = AuraProgress.Shift + 1;
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ cinematics
        private static Vector3 SceneCenter(Camera main)
        {
            Bounds b = new Bounds(Vector3.zero, Vector3.one);
            bool has = false;
            Renderer[] rs = FindObjectsOfType<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<Canvas>() != null) continue;
                Vector3 s = r.bounds.size;
                if (s.x > 60f || s.z > 60f) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) return main.transform.position + main.transform.forward * 12f;
            return b.center;
        }

        private Camera MakeCine(Camera main)
        {
            GameObject go = new GameObject("Aura_CineCam");
            Camera c = go.AddComponent<Camera>();
            c.CopyFrom(main);
            c.depth = main.depth + 10f;
            c.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            return c;
        }

        private void DestroyCine()
        {
            if (cine != null) Destroy(cine.gameObject);
            cine = null;
        }

        private IEnumerator IntroRoutine()
        {
            Camera main = Camera.main;
            if (main == null) yield break;

            cine = MakeCine(main);
            Vector3 center = SceneCenter(main);
            float baseFov = main.fieldOfView;
            float baseOrtho = main.orthographicSize;

            barTarget = 78f;
            StartCoroutine(TitleCard("SHIFT " + AuraProgress.Shift, Tips[UnityEngine.Random.Range(0, Tips.Length)], 3.3f));

            const float dur = 3.4f;
            float t = 0f;
            while (t < dur && cine != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = k * k * (3f - 2f * k);

                // read the live gameplay camera each frame so the hand-off is seamless
                Vector3 endPos = main.transform.position;
                Quaternion endRot = main.transform.rotation;
                Vector3 rel = endPos - center;

                Vector3 pos = center + Quaternion.AngleAxis(Mathf.Lerp(80f, 0f, e), Vector3.up) * rel + Vector3.up * Mathf.Lerp(3.5f, 0f, e);
                Vector3 look = Vector3.Lerp(center, endPos + endRot * Vector3.forward * rel.magnitude, e);
                Vector3 dir = look - pos;
                Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : endRot;

                cine.transform.SetPositionAndRotation(pos, rot);
                if (cine.orthographic) cine.orthographicSize = Mathf.Lerp(baseOrtho * 1.35f, baseOrtho, e);
                else cine.fieldOfView = Mathf.Lerp(baseFov * 1.25f, baseFov, e);
                yield return null;
            }
            FinishIntro();
        }

        private void FinishIntro()
        {
            if (introCo != null) { StopCoroutine(introCo); introCo = null; }
            DestroyCine();
            barTarget = 0f;
        }

        private IEnumerator OutroRoutine()
        {
            Camera main = Camera.main;
            barTarget = 78f;
            StartCoroutine(BigBanner("TIME'S UP!", AuraTheme.Gold, 2.0f));
            if (main == null) yield break;

            cine = MakeCine(main);
            Vector3 center = SceneCenter(main);
            Vector3 startPos = main.transform.position;
            Quaternion startRot = main.transform.rotation;
            Vector3 rel = startPos - center;
            float t = 0f;
            // a slow, slightly dizzy pull-back that keeps drifting behind the results card
            while (cine != null)
            {
                t += Time.unscaledDeltaTime;
                float e = 1f - Mathf.Exp(-t * 0.8f);
                Vector3 pos = center + Quaternion.AngleAxis(-28f * e, Vector3.up) * (rel * (1f + 0.35f * e)) + Vector3.up * (2.5f * e);
                Quaternion rot = Quaternion.LookRotation((center - pos).normalized);
                cine.transform.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, rot, Mathf.Clamp01(e * 1.5f)));
                yield return null;
            }
        }

        // ------------------------------------------------------------------ deliveries: ratings, tips, juice
        private void OnPlaced(object sender, MenuRecipeSO recipe)
        {
            placed.Add(new KeyValuePair<MenuRecipeSO, float>(recipe, Time.time));
        }

        private void OnDelivered(object sender, MenuRecipeSO recipe)
        {
            float age = 20f;
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].Key != recipe) continue;
                age = Time.time - placed[i].Value;
                placed.RemoveAt(i);
                break;
            }

            int stars = age <= 15f ? 3 : (age <= 30f ? 2 : 1);
            string mood = stars == 3 ? "DELIGHTED!" : (stars == 2 ? "HAPPY" : "FINE");
            int tip = Mathf.RoundToInt(stars * 2f * AuraUpgrades.TipMult);
            if (ScoreBonusMult > 1) tip *= 2;

            if (vipActive)
            {
                vipActive = false;
                tip += 25;
                mood = "VIP SATISFIED!";
                if (AuraGameMechanics.Instance != null) AuraGameMechanics.Instance.AddBonusScore(400);
                if (GameManager.Instance != null) GameManager.Instance.AddBonusTime(10f);
                HideEvent();
            }

            shiftCoins += tip;
            StartCoroutine(RatingPopup(stars, mood, tip));
            StartCoroutine(HitStop(0.12f));
            StartCoroutine(Flash(stars == 3 ? new Color(1f, 0.9f, 0.5f, 0.38f) : new Color(1f, 1f, 1f, 0.2f)));
        }

        private IEnumerator HitStop(float real)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) yield break;
            float before = Time.timeScale;
            if (before < 0.99f) yield break;
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(real);
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying && Time.timeScale < 0.99f) Time.timeScale = 1f;
        }

        private IEnumerator Flash(Color c)
        {
            if (flash == null) yield break;
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                flash.color = new Color(c.r, c.g, c.b, c.a * (1f - t / 0.3f));
                yield return null;
            }
            flash.color = Color.clear;
        }

        private IEnumerator RatingPopup(int stars, string mood, int tip)
        {
            if (canvas == null) yield break;
            RectTransform root = AuraTheme.MakeRect(canvas.transform, "RatingPopup");
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.3f);
            root.sizeDelta = new Vector2(520, 150);
            CanvasGroup cg = root.gameObject.AddComponent<CanvasGroup>();

            for (int i = 0; i < 3; i++)
            {
                Image s = AuraTheme.CreateIcon(root, AuraIcons.Get(AuraIcon.Star), new Vector2(-70 + i * 70, 40), 64f,
                    i < stars ? AuraTheme.Gold : new Color(0f, 0f, 0f, 0.45f));
                s.rectTransform.localRotation = Quaternion.Euler(0, 0, (i - 1) * -10f);
            }
            AuraTheme.CreateText(root, mood + "   +" + tip + " coins", 36, FontStyles.Normal, new Vector2(0, -28), new Vector2(520, 50),
                stars == 3 ? AuraTheme.Gold : AuraTheme.Paper);

            float t = 0f;
            const float life = 1.7f;
            while (t < life && root != null)
            {
                t += Time.unscaledDeltaTime;
                float k = t / life;
                float pop = k < 0.18f ? Mathf.Lerp(0.4f, 1.12f, k / 0.18f) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((k - 0.18f) / 0.12f));
                root.localScale = Vector3.one * pop;
                root.anchoredPosition = new Vector2(0, 70f * k);
                cg.alpha = k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
                yield return null;
            }
            if (root != null) Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------ shift events
        private IEnumerator EventLoop()
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(12f, 20f));
            int last = -1;
            while (!ended)
            {
                yield return new WaitUntil(() => ended || (GameManager.Instance != null && GameManager.Instance.IsPlaying));
                if (ended) yield break;

                int pick;
                do { pick = UnityEngine.Random.Range(0, 3); } while (pick == last);
                last = pick;
                yield return RunEvent(pick);
                yield return new WaitForSeconds(UnityEngine.Random.Range(16f, 26f));
            }
        }

        private IEnumerator RunEvent(int kind)
        {
            string title, sub;
            Color col;
            AuraIcon icon;
            float dur;
            switch (kind)
            {
                case 0:
                    title = "RUSH HOUR"; sub = "Orders pour in. Double points!";
                    col = AuraTheme.Danger; icon = AuraIcon.Flame; dur = 14f;
                    eventSpawn = 2f; ScoreBonusMult = 2;
                    break;
                case 1:
                    title = "SUGAR RUSH"; sub = "Sweet energy. You run faster!";
                    col = AuraTheme.Amber; icon = AuraIcon.Star; dur = 12f;
                    SpeedBonus = 1.35f;
                    break;
                default:
                    title = "VIP GUEST"; sub = "Serve the next dish for +400 and +10s";
                    col = AuraTheme.Gold; icon = AuraIcon.Crown; dur = 25f;
                    vipActive = true;
                    break;
            }

            ShowEvent(title, sub, col, icon, dur);
            StartCoroutine(BigBanner(title, col, 1.5f));

            eventRemaining = dur;
            while (eventRemaining > 0f && !ended)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsPlaying) eventRemaining -= Time.deltaTime;
                if (kind == 2 && !vipActive) break; // served
                yield return null;
            }

            if (kind == 2 && vipActive)
            {
                vipActive = false;
                StartCoroutine(BigBanner("VIP LEFT...", AuraTheme.Danger, 1.2f));
            }
            eventSpawn = 1f;
            ScoreBonusMult = 1;
            SpeedBonus = 1f;
            HideEvent();
        }

        private void ShowEvent(string title, string sub, Color col, AuraIcon icon, float dur)
        {
            if (eventPanel == null) return;
            eventTotal = dur;
            eventRemaining = dur;
            eventTitle.text = title;
            eventSub.text = sub;
            eventTitle.color = col;
            eventBar.color = col;
            eventIcon.sprite = AuraIcons.Get(icon);
            eventIcon.color = col;
            eventPanel.SetActive(true);
        }

        private void HideEvent()
        {
            if (eventPanel != null) eventPanel.SetActive(false);
            eventTotal = 0f;
        }

        // ------------------------------------------------------------------ banners & title card
        private IEnumerator BigBanner(string text, Color col, float life)
        {
            if (canvas == null) yield break;
            RectTransform rt = AuraTheme.MakeRect(canvas.transform, "BigBanner");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.62f);
            rt.sizeDelta = new Vector2(1200, 180);
            CanvasGroup cg = rt.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI t = AuraTheme.CreateText(rt, text, 120, FontStyles.Normal, Vector2.zero, new Vector2(1200, 170), col);
            t.outlineWidth = 0.32f;

            float el = 0f;
            while (el < life && rt != null)
            {
                el += Time.unscaledDeltaTime;
                float k = el / life;
                float s = k < 0.2f ? Mathf.Lerp(2.2f, 1f, 1f - Mathf.Pow(1f - k / 0.2f, 3f)) : 1f + 0.04f * (k - 0.2f);
                rt.localScale = Vector3.one * s;
                cg.alpha = k < 0.1f ? k / 0.1f : (k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        private IEnumerator TitleCard(string title, string sub, float life)
        {
            if (titleGroup == null) yield break;
            titleText.text = title;
            titleSub.text = sub;
            float el = 0f;
            while (el < life)
            {
                el += Time.unscaledDeltaTime;
                float k = el / life;
                titleGroup.alpha = k < 0.15f ? k / 0.15f : (k > 0.8f ? 1f - (k - 0.8f) / 0.2f : 1f);
                float slide = Mathf.Lerp(-140f, 0f, 1f - Mathf.Pow(1f - Mathf.Clamp01(k / 0.25f), 3f));
                titleRect.anchoredPosition = new Vector2(slide, 0f);
                yield return null;
            }
            titleGroup.alpha = 0f;
        }

        // ------------------------------------------------------------------ ui construction
        private void BuildUI()
        {
            GameObject go = new GameObject("Aura_Showtime_Canvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 480;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<AuraSkinned>();

            barTop = MakeBar("BarTop", true);
            barBottom = MakeBar("BarBottom", false);

            // full-screen flash
            RectTransform fr = AuraTheme.MakeRect(go.transform, "Flash");
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            flash = fr.gameObject.AddComponent<Image>();
            flash.color = Color.clear;
            flash.raycastTarget = false;

            // title card
            titleRect = AuraTheme.MakeRect(go.transform, "TitleCard");
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(1400, 300);
            titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            titleText = AuraTheme.CreateText(titleRect, "SHIFT 1", 170, FontStyles.Normal, new Vector2(0, 40), new Vector2(1400, 200), AuraTheme.Gold);
            titleText.outlineWidth = 0.32f;
            titleSub = AuraTheme.CreateText(titleRect, "", 40, FontStyles.Normal, new Vector2(0, -90), new Vector2(1400, 60), AuraTheme.Paper);

            // event banner (under the timer)
            eventPanel = AuraTheme.CreatePanel(go.transform, "EventBanner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(720, 112), AuraTheme.PanelFill, AuraTheme.Stroke);
            eventIcon = AuraTheme.CreateIcon(eventPanel.transform, AuraIcons.Get(AuraIcon.Flame), new Vector2(-300, 8), 62f, AuraTheme.Gold);
            eventTitle = AuraTheme.CreateText(eventPanel.transform, "EVENT", 40, FontStyles.Normal, new Vector2(30, 24), new Vector2(560, 50), AuraTheme.Gold, TextAlignmentOptions.Left);
            eventSub = AuraTheme.CreateText(eventPanel.transform, "", 22, FontStyles.Normal, new Vector2(30, -16), new Vector2(560, 34), AuraTheme.TextSecondary, TextAlignmentOptions.Left);

            RectTransform track = AuraTheme.MakeRect(eventPanel.transform, "BarTrack");
            track.anchorMin = new Vector2(0f, 0f); track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(26, 12); track.offsetMax = new Vector2(-26, 24);
            Image tr = track.gameObject.AddComponent<Image>();
            tr.sprite = AuraTheme.RoundedFill; tr.type = Image.Type.Sliced; tr.color = new Color(0f, 0f, 0f, 0.4f); tr.raycastTarget = false;

            RectTransform fill = AuraTheme.MakeRect(track, "Fill");
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            eventBar = fill.gameObject.AddComponent<Image>();
            eventBar.sprite = AuraTheme.RoundedFill;
            eventBar.type = Image.Type.Filled;
            eventBar.fillMethod = Image.FillMethod.Horizontal;
            eventBar.raycastTarget = false;

            eventPanel.SetActive(false);
        }

        private RectTransform MakeBar(string name, bool top)
        {
            RectTransform r = AuraTheme.MakeRect(canvas.transform, name);
            r.anchorMin = new Vector2(0f, top ? 1f : 0f);
            r.anchorMax = new Vector2(1f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, top ? 1f : 0f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(0f, 0f);
            Image im = r.gameObject.AddComponent<Image>();
            im.color = new Color(0.08f, 0.03f, 0.01f, 1f);
            im.raycastTarget = false;
            return r;
        }
    }
}
