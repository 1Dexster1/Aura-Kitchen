using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AuraKitchen
{
    public class Aura3DLobbyShowcase : MonoBehaviour
    {
        public static Aura3DLobbyShowcase Instance { get; private set; }

        [Header("Squad Stage Config")]
        [SerializeField] private Vector3 stageCenter = new Vector3(0.95f, 0f, -4.2f);
        private const float BaseYRotation = 18f;

        private GameObject stageRootObj;
        private SquadSlotData[] squadSlots = new SquadSlotData[4];
        private List<GameObject> originalSceneDucks = new List<GameObject>();

        // Cartoon backdrop (dark floor, halftone dots, floating kitchen icons)
        private GameObject backdropRoot;
        private readonly List<Transform> backdropIcons = new List<Transform>();
        private readonly List<Vector3> backdropIconBase = new List<Vector3>();
        private CameraClearFlags savedClearFlags;
        private Color savedBackground;
        private bool cameraSaved;

        private float targetRotationY = 0f;
        private float currentRotationY = 0f;
        private bool isDragging = false;
        private Vector2 lastTouchPos;
        private float idleReturnTimer = 0f;

        public class SquadSlotData
        {
            public int slotIndex;
            public GameObject pedestalObj;
            public GameObject neonRingObj;
            public GameObject chefModelObj;
            public Light spotLight;
            public TextMeshPro overhead3DText;
            public LobbyChefIdleAnimator idleAnimator;
            public bool isOccupied;
            public string chefName;
        }

        public static readonly Color[] SlotNeonColors = AuraTheme.ChefColors;

        // Battle-Royale V-Formation offsets (relative to stageCenter)
        private static readonly Vector3[] SlotPositions = new Vector3[]
        {
            new Vector3(-0.95f, 0f, 0f),    // Slot 0: Host / Center-Left Front
            new Vector3(0.95f, 0f, 0f),     // Slot 1: P2 / Center-Right Front
            new Vector3(-2.25f, 0f, 0.85f), // Slot 2: P3 / Left Flank
            new Vector3(2.25f, 0f, 0.85f)   // Slot 3: P4 / Right Flank
        };

        private static readonly float[] SlotYRotations = new float[]
        {
            10f,   // Slot 0 angles slightly inward
            -10f,  // Slot 1 angles slightly inward
            22f,   // Slot 2 angles inward
            -22f   // Slot 3 angles inward
        };

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildSquadStage();
        }

        private void Start()
        {
            // Initialize with Host (Slot 0) active
            string myName = PlayerPrefs.GetString("Aura_PlayerName", "Chef Ruby");
            UpdateSlot(0, true, myName, true);
            UpdateSlot(1, false, "[OPEN]", false);
            UpdateSlot(2, false, "[OPEN]", false);
            UpdateSlot(3, false, "[OPEN]", false);

            // Initially hidden until user enters Multiplayer
            SetStageVisible(false);
        }

        private void BuildSquadStage()
        {
            stageRootObj = new GameObject("Aura_3D_Squad_Stage");
            stageRootObj.transform.position = stageCenter;
            stageRootObj.transform.rotation = Quaternion.Euler(0, BaseYRotation, 0);

            // 1. Dark Championship Stage Floor Platform
            GameObject arenaBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arenaBase.name = "Arena_Floor_Base";
            arenaBase.transform.SetParent(stageRootObj.transform, false);
            arenaBase.transform.localPosition = new Vector3(0, 0.015f, 0.4f);
            arenaBase.transform.localScale = new Vector3(6.8f, 0.015f, 4.8f);
            Destroy(arenaBase.GetComponent<Collider>());

            Renderer aRend = arenaBase.GetComponent<Renderer>();
            Material aMat = new Material(GetDefaultShader());
            aMat.color = new Color(0.08f, 0.08f, 0.10f);
            if (aMat.HasProperty("_BaseColor")) aMat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.10f));
            if (aMat.HasProperty("_Metallic")) aMat.SetFloat("_Metallic", 0.5f);
            if (aMat.HasProperty("_Smoothness")) aMat.SetFloat("_Smoothness", 0.7f);
            aRend.material = aMat;

            BuildBackdrop();

            // Soft contact shadow: removes the hard "sticker" edge of the platform on bright floors
            GameObject shadowObj = new GameObject("Arena_Soft_Shadow");
            shadowObj.transform.SetParent(stageRootObj.transform, false);
            shadowObj.transform.localPosition = new Vector3(0f, 0.024f, 0.4f);
            shadowObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowObj.transform.localScale = new Vector3(11f, 8f, 1f);
            SpriteRenderer shadowRend = shadowObj.AddComponent<SpriteRenderer>();
            shadowRend.sprite = MakeSoftDisc();
            shadowRend.color = new Color(0.02f, 0.04f, 0.1f, 0.55f);
            shadowRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadowRend.receiveShadows = false;
            shadowRend.sortingOrder = 5;

            // 2. Build 4 Pedestals
            for (int i = 0; i < 4; i++)
            {
                squadSlots[i] = CreatePodiumStation(i);
            }
        }

        private static Sprite MakeHalftoneSprite()
        {
            const int n = 256;
            const int cell = 16;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[n * n];
            Color ink = AuraTheme.Ink;
            Color dotColor = new Color(0.20f, 0.20f, 0.24f, 1f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // dot grid, every other row shifted by half a cell (classic comic halftone)
                    int shift = ((y / cell) % 2) * (cell / 2);
                    float fx = ((x + shift) % cell) - cell * 0.5f;
                    float fy = (y % cell) - cell * 0.5f;
                    float d = Mathf.Sqrt(fx * fx + fy * fy);
                    float dot = 1f - Mathf.SmoothStep(1.8f, 3.0f, d);
                    px[y * n + x] = Color.Lerp(ink, dotColor, dot);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
        }

        private void BuildBackdrop()
        {
            backdropRoot = new GameObject("Aura_Lobby_Backdrop");
            backdropRoot.transform.position = stageCenter;

            // Dark halftone ground that swallows the bright tile floor (tiled sprite: 8-unit tiles)
            GameObject ground = new GameObject("Backdrop_Ground");
            ground.transform.SetParent(backdropRoot.transform, false);
            ground.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SpriteRenderer gr = ground.AddComponent<SpriteRenderer>();
            gr.sprite = MakeHalftoneSprite();
            gr.drawMode = SpriteDrawMode.Tiled;
            gr.size = new Vector2(120f, 120f);
            gr.sortingOrder = 0;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gr.receiveShadows = false;

            // Big faint kitchen icons floating behind the squad (billboarded in Update)
            AuraIcon[] icons = { AuraIcon.ChefHat, AuraIcon.Cloche, AuraIcon.Flame, AuraIcon.Plate, AuraIcon.Star, AuraIcon.Clock, AuraIcon.Crown };
            Vector3[] spots =
            {
                new Vector3(-7.5f, 3.4f, 7.5f), new Vector3(-4.2f, 5.2f, 9.5f), new Vector3(-0.5f, 3.0f, 10.5f),
                new Vector3(3.6f, 5.0f, 9.5f), new Vector3(7.2f, 3.6f, 7.8f), new Vector3(-9.5f, 5.4f, 4.5f), new Vector3(9.6f, 5.4f, 4.8f)
            };
            float[] sizes = { 3.2f, 2.4f, 3.0f, 2.6f, 2.2f, 2.0f, 2.2f };

            for (int i = 0; i < icons.Length; i++)
            {
                GameObject go = new GameObject("Backdrop_Icon_" + icons[i]);
                go.transform.SetParent(backdropRoot.transform, false);
                go.transform.localPosition = spots[i];
                go.transform.localScale = Vector3.one * sizes[i];
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = AuraIcons.Get(icons[i]);
                sr.color = new Color(AuraTheme.Paper.r, AuraTheme.Paper.g, AuraTheme.Paper.b, 0.16f);
                sr.sortingOrder = 2;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                backdropIcons.Add(go.transform);
                backdropIconBase.Add(spots[i]);
            }
        }

        private void EnterCartoonCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (!cameraSaved)
            {
                savedClearFlags = cam.clearFlags;
                savedBackground = cam.backgroundColor;
                cameraSaved = true;
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = AuraTheme.Ink;
        }

        private void RestoreCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || !cameraSaved) return;
            cam.clearFlags = savedClearFlags;
            cam.backgroundColor = savedBackground;
            cameraSaved = false;
        }

        private void OnDestroy()
        {
            RestoreCamera();
            if (Instance == this) Instance = null;
        }

        private static Sprite softDisc;

        private static Sprite MakeSoftDisc()
        {
            if (softDisc != null) return softDisc;
            const int n = 128;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (n - 1f)) * 2f - 1f;
                    float dy = (y / (n - 1f)) * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.SmoothStep(0.25f, 1f, d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            softDisc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 2f);
            return softDisc;
        }

        private static Shader GetDefaultShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Mobile/Diffuse");
            if (s == null) s = Shader.Find("Diffuse");
            return s;
        }

        private SquadSlotData CreatePodiumStation(int slot)
        {
            SquadSlotData data = new SquadSlotData();
            data.slotIndex = slot;

            GameObject stationRoot = new GameObject($"Station_Slot_{slot}");
            stationRoot.transform.SetParent(stageRootObj.transform, false);
            stationRoot.transform.localPosition = SlotPositions[slot];
            stationRoot.transform.localRotation = Quaternion.Euler(0, SlotYRotations[slot], 0);

            // 1. Dark Cylindrical Platform / Pedestal
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal_Base";
            pedestal.transform.SetParent(stationRoot.transform, false);
            pedestal.transform.localPosition = new Vector3(0, 0.06f, 0);
            pedestal.transform.localScale = new Vector3(1.4f, 0.06f, 1.4f);
            Destroy(pedestal.GetComponent<Collider>());

            Renderer pRend = pedestal.GetComponent<Renderer>();
            Material pMat = new Material(GetDefaultShader());
            pMat.color = new Color(0.12f, 0.12f, 0.14f);
            if (pMat.HasProperty("_BaseColor")) pMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.14f));
            if (pMat.HasProperty("_Metallic")) pMat.SetFloat("_Metallic", 0.6f);
            if (pMat.HasProperty("_Smoothness")) pMat.SetFloat("_Smoothness", 0.8f);
            pRend.material = pMat;
            data.pedestalObj = pedestal;

            // 2. Glowing Neon Emission Ring on top rim
            GameObject neonRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neonRing.name = "Neon_Ring";
            neonRing.transform.SetParent(stationRoot.transform, false);
            neonRing.transform.localPosition = new Vector3(0, 0.125f, 0);
            neonRing.transform.localScale = new Vector3(1.46f, 0.012f, 1.46f);
            Destroy(neonRing.GetComponent<Collider>());

            Renderer nRend = neonRing.GetComponent<Renderer>();
            Material nMat = new Material(GetDefaultShader());
            Color neonColor = SlotNeonColors[slot];
            nMat.color = neonColor;
            if (nMat.HasProperty("_BaseColor")) nMat.SetColor("_BaseColor", neonColor);
            if (nMat.HasProperty("_EmissionColor"))
            {
                nMat.EnableKeyword("_EMISSION");
                nMat.SetColor("_EmissionColor", neonColor * 2.2f);
            }
            nRend.material = nMat;
            data.neonRingObj = neonRing;

            // 3. Upward Colored Underlight
            GameObject lightObj = new GameObject("Podium_Underlight");
            lightObj.transform.SetParent(stationRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0, 0.2f, 0);
            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = neonColor;
            pLight.range = 3.2f;
            pLight.intensity = 1.8f;
            pLight.shadows = LightShadows.None;
            data.spotLight = pLight;

            // 4. Instantiate 3D Chef Model
            GameObject chef = SpawnChefModel(slot, stationRoot.transform);
            data.chefModelObj = chef;
            if (chef != null)
            {
                data.idleAnimator = chef.AddComponent<LobbyChefIdleAnimator>();
                data.idleAnimator.Initialize(slot);
            }

            // 5. 3D Floating Nameplate Above Head (compact: 3 short lines, well above the head)
            GameObject tagObj = new GameObject("Overhead_3D_Tag");
            tagObj.transform.SetParent(stationRoot.transform, false);
            tagObj.transform.localPosition = new Vector3(0, 2.5f, 0);

            TextMeshPro tmp = tagObj.AddComponent<TextMeshPro>();
            if (AuraTheme.Font != null) tmp.font = AuraTheme.Font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 1.7f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color32(6, 10, 22, 255);
            tmp.rectTransform.sizeDelta = new Vector2(2.2f, 0.9f);
            data.overhead3DText = tmp;

            return data;
        }

        private GameObject SpawnChefModel(int slot, Transform parent)
        {
            GameObject prefab = Resources.Load<GameObject>("PlayerVisual");
            GameObject chefObj = null;

            if (prefab != null)
            {
                chefObj = Instantiate(prefab, parent, false);
            }
            else
            {
                // Fallback: look for any PlayerVisual in scene to clone
                GameObject sceneDuck = GameObject.Find("PlayerVisual");
                if (sceneDuck != null)
                {
                    chefObj = Instantiate(sceneDuck, parent, false);
                    chefObj.SetActive(true);
                }
            }

            if (chefObj == null) return null;

            chefObj.name = $"ChefModel_P{slot + 1}";
            chefObj.transform.localPosition = new Vector3(0, 0.13f, 0);
            chefObj.transform.localRotation = Quaternion.identity;
            chefObj.transform.localScale = Vector3.one * 1.25f;

            MeshRenderer[] renderers = chefObj.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.gameObject.name == "Head" || r.gameObject.name == "Body" ||
                    (r.sharedMaterial != null && r.sharedMaterial.name.Contains("PlayerBody")))
                {
                    Material instMat = r.sharedMaterial != null
                        ? new Material(r.sharedMaterial)
                        : new Material(GetDefaultShader());
                    instMat.color = SlotNeonColors[slot];
                    if (instMat.HasProperty("_BaseColor")) instMat.SetColor("_BaseColor", SlotNeonColors[slot]);
                    if (instMat.HasProperty("_Color")) instMat.SetColor("_Color", SlotNeonColors[slot]);
                    r.material = instMat;
                }
            }

            return chefObj;
        }

        public void UpdateSlot(int slot, bool occupied, string name, bool isReady)
        {
            if (slot < 0 || slot >= squadSlots.Length) return;
            SquadSlotData data = squadSlots[slot];
            if (data == null) return;

            data.isOccupied = occupied;
            data.chefName = name;

            string[] slotRoles = { "HOST", "P2", "P3", "P4" };
            string colorHex = ColorUtility.ToHtmlStringRGB(SlotNeonColors[slot]);
            string dim = ColorUtility.ToHtmlStringRGB(AuraTheme.TextSecondary);

            if (occupied)
            {
                if (data.chefModelObj != null) data.chefModelObj.SetActive(true);
                if (data.spotLight != null) data.spotLight.intensity = 2.2f;

                if (data.overhead3DText != null)
                {
                    string shown = ShortName(string.IsNullOrEmpty(name) ? AuraTheme.ChefName(slot) : name);
                    string readyStr = isReady
                        ? "<color=#" + ColorUtility.ToHtmlStringRGB(AuraTheme.Success) + ">READY</color>"
                        : "<color=#" + ColorUtility.ToHtmlStringRGB(AuraTheme.Amber) + ">WAITING</color>";
                    data.overhead3DText.text =
                        "<size=62%><color=#" + colorHex + ">" + slotRoles[slot] + "</color></size>\n" +
                        "<size=100%>" + shown + "</size>\n" +
                        "<size=62%>" + readyStr + "</size>";
                }
            }
            else
            {
                // Slot is open: hide chef model, show a quiet invite marker
                if (data.chefModelObj != null) data.chefModelObj.SetActive(false);
                if (data.spotLight != null) data.spotLight.intensity = 0.8f;

                if (data.overhead3DText != null)
                {
                    data.overhead3DText.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                    data.overhead3DText.text =
                        "<size=62%><color=#" + dim + ">" + slotRoles[slot] + "</color></size>\n" +
                        "<size=78%><color=#" + ColorUtility.ToHtmlStringRGB(AuraTheme.Amber) + ">+ INVITE</color></size>";
                }
            }

            if (occupied && data.overhead3DText != null)
            {
                data.overhead3DText.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            }
        }

        private static string ShortName(string raw)
        {
            string n = (raw ?? "").Trim();
            // names come from other players: strip rich-text tags so they can't restyle the label
            n = n.Replace("<", "").Replace(">", "");
            if (n.Length > 11) n = n.Substring(0, 10) + "\u2026";
            return n.ToUpperInvariant();
        }

        public void SetStageVisible(bool visible)
        {
            if (stageRootObj != null)
            {
                stageRootObj.SetActive(visible);
            }
            if (backdropRoot != null) backdropRoot.SetActive(visible);
            if (visible) EnterCartoonCamera(); else RestoreCamera();

            CacheAndToggleOriginalDucks(!visible);
        }

        private void CacheAndToggleOriginalDucks(bool show)
        {
            try
            {
                if (originalSceneDucks.Count == 0)
                {
                    foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        foreach (var t in rootGo.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.name.StartsWith("PlayerVisual") && (stageRootObj == null || !t.IsChildOf(stageRootObj.transform)))
                            {
                                originalSceneDucks.Add(t.gameObject);
                            }
                        }
                    }
                }

                foreach (var duck in originalSceneDucks)
                {
                    if (duck != null) duck.SetActive(show);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Aura3D] CacheAndToggleOriginalDucks exception: {ex.Message}");
            }
        }

        private void Update()
        {
            HandleSwipeRotation();

            // Billboard 3D nameplates to face camera
            if (Camera.main != null)
            {
                Quaternion camRot = Camera.main.transform.rotation;
                for (int i = 0; i < backdropIcons.Count; i++)
                {
                    if (backdropIcons[i] == null) continue;
                    backdropIcons[i].rotation = camRot * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.4f + i) * 6f);
                    Vector3 b = backdropIconBase[i];
                    backdropIcons[i].localPosition = new Vector3(b.x, b.y + Mathf.Sin(Time.time * 0.7f + i * 1.3f) * 0.25f, b.z);
                }
                for (int i = 0; i < squadSlots.Length; i++)
                {
                    if (squadSlots[i] != null && squadSlots[i].overhead3DText != null)
                    {
                        squadSlots[i].overhead3DText.transform.rotation = camRot;
                    }
                }
            }
        }

        // PUBG / Free Fire style touch swipe to rotate the 3D squad stage
        private void HandleSwipeRotation()
        {
            if (stageRootObj == null) return;

            // Touch Input (Mobile)
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.position.y > Screen.height * 0.22f && t.position.y < Screen.height * 0.82f)
                {
                    if (t.phase == TouchPhase.Began)
                    {
                        isDragging = true;
                        lastTouchPos = t.position;
                        idleReturnTimer = 0f;
                    }
                    else if (t.phase == TouchPhase.Moved && isDragging)
                    {
                        float deltaX = t.position.x - lastTouchPos.x;
                        lastTouchPos = t.position;
                        targetRotationY -= deltaX * 0.28f;
                        targetRotationY = Mathf.Clamp(targetRotationY, -55f, 55f);
                        idleReturnTimer = 0f;
                    }
                    else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        isDragging = false;
                    }
                }
            }
            // Mouse Input (Editor / PC)
            else if (Input.GetMouseButtonDown(0))
            {
                if (Input.mousePosition.y > Screen.height * 0.22f && Input.mousePosition.y < Screen.height * 0.82f)
                {
                    isDragging = true;
                    lastTouchPos = Input.mousePosition;
                    idleReturnTimer = 0f;
                }
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                float deltaX = (float)Input.mousePosition.x - lastTouchPos.x;
                lastTouchPos = Input.mousePosition;
                targetRotationY -= deltaX * 0.35f;
                targetRotationY = Mathf.Clamp(targetRotationY, -55f, 55f);
                idleReturnTimer = 0f;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            // Smooth return to front after 2.5s of no touch
            if (!isDragging)
            {
                idleReturnTimer += Time.deltaTime;
                if (idleReturnTimer > 2.5f)
                {
                    targetRotationY = Mathf.Lerp(targetRotationY, 0f, Time.deltaTime * 2.5f);
                }
            }

            // Interpolate rotation
            currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, Time.deltaTime * 12f);
            stageRootObj.transform.rotation = Quaternion.Euler(0, BaseYRotation + currentRotationY, 0);
        }
    }

    // Procedural Idle Animator giving organic life to the 3D chefs on the podiums
    public class LobbyChefIdleAnimator : MonoBehaviour
    {
        private Vector3 initialScale;
        private Vector3 initialPos;
        private float seed;
        private float breatheTimer;
        private float lookTimer;
        private float targetHeadAngle = 0f;
        private float currentHeadAngle = 0f;

        public void Initialize(int slot)
        {
            initialScale = transform.localScale;
            initialPos = transform.localPosition;
            seed = slot * 1.37f;
            breatheTimer = seed;
            lookTimer = UnityEngine.Random.Range(1f, 3.5f);
        }

        private void Update()
        {
            breatheTimer += Time.deltaTime * 2.8f;

            // 1. Organic Breathing (Squash & Stretch)
            float breathe = Mathf.Sin(breatheTimer) * 0.035f;
            transform.localScale = new Vector3(initialScale.x * (1f - breathe * 0.5f),
                                               initialScale.y * (1f + breathe),
                                               initialScale.z * (1f - breathe * 0.5f));

            // 2. Gentle Floating/Bobbing
            float bob = Mathf.Sin(breatheTimer * 1.2f) * 0.025f;
            transform.localPosition = new Vector3(initialPos.x, initialPos.y + bob, initialPos.z);

            // 3. Natural Head Look-Around
            lookTimer -= Time.deltaTime;
            if (lookTimer <= 0f)
            {
                targetHeadAngle = UnityEngine.Random.Range(-18f, 18f);
                lookTimer = UnityEngine.Random.Range(2.5f, 5f);
            }
            currentHeadAngle = Mathf.Lerp(currentHeadAngle, targetHeadAngle, Time.deltaTime * 4f);
            transform.localRotation = Quaternion.Euler(0, currentHeadAngle, 0);
        }
    }
}
