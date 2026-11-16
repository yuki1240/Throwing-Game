using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// Christmas シーンの起動用オブジェクト。ボールが壁に当たるたびにクリスマスツリーへ飾りを1つずつ付け、
    /// 15回で「クリスマスツリーの完成～！」を5秒出し、カウントダウンのあと次の場面（草原）へ移ります。
    /// 遊ぶのは幼稚園の子どもなので、文字は大きく、ひらがなを多くしています。
    /// </summary>
    public sealed class ChristmasBootstrap : MonoBehaviour
    {
        /// <summary>次の場面（草原）のシーン名。</summary>
        public const string NextSceneName = "Game";

        /// <summary>校正のシーン名。</summary>
        public const string CalibrationSceneName = "Calibration";

        /// <summary>お祝いの演出を出す間隔（秒）。</summary>
        public const float CelebrationBurstInterval = 0.35f;

        private static readonly Color[] OrnamentColors =
        {
            new Color(0.95f, 0.2f, 0.25f),
            new Color(1f, 0.82f, 0.2f),
            new Color(0.25f, 0.55f, 1f),
            new Color(1f, 0.45f, 0.75f),
            new Color(0.7f, 0.4f, 1f),
            new Color(1f, 0.6f, 0.15f),
            new Color(0.9f, 0.95f, 1f)
        };

        private static readonly Color StarDimColor = new Color(0.75f, 0.68f, 0.35f, 1f);
        private static readonly Color StarBrightColor = new Color(1f, 0.9f, 0.25f, 1f);
        private static readonly Color GoldText = new Color(1f, 0.92f, 0.35f, 1f);

        private readonly System.Random random = new System.Random();
        private readonly ChristmasFlow flow = new ChristmasFlow();
        private CalibrationSession session;
        private List<Vector2> slots;
        private int[] slotOrder;
        private RectTransform ornamentLayer;
        private EffectSpawner spawner;
        private Image star;
        private Text instructionText;
        private Text counterText;
        private Text celebrationText;
        private Text countdownTitleText;
        private Text countdownNumberText;
        private Text operatorText;
        private GameObject overlay;
        private float nextBurstAt;
        private bool sceneRequested;

        private void Start()
        {
            session = CalibrationSession.EnsureExists();
            EventSystemKeeper.Ensure();
            slots = ChristmasTreeLayout.GetOrnamentSlots();
            slotOrder = ChristmasTreeLayout.GetShuffledOrder(slots.Count, random);
            BuildScreen();
            RefreshTexts();
        }

        private void Update()
        {
            session = CalibrationSession.EnsureExists();
            HandleKeyboard();
            HandleHits();
            flow.Tick(Time.deltaTime);
            AnimateCelebration();
            RefreshTexts();
            GoToNextSceneWhenFinished();
        }

        private void BuildScreen()
        {
            Canvas canvas = UiFactory.CreateCanvas("ChristmasCanvas", 0);
            BuildSky(canvas.transform);
            BuildSnowGround(canvas.transform);
            BuildSnowFall(canvas.transform);
            BuildTree(canvas.transform);
            ornamentLayer = CreateFullLayer(canvas.transform, "Ornaments");
            star = BuildStar(canvas.transform);
            spawner = BuildEffectLayer(canvas.transform);
            BuildKidsTexts(canvas.transform);
            BuildOperatorOverlay(canvas.transform);
        }

        private static RectTransform CreateFullLayer(Transform parent, string name)
        {
            RectTransform rect = UiFactory.CreateRect(parent, name);
            UiFactory.Stretch(rect);
            return rect;
        }

        private void BuildSky(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Sky");
            RawImage sky = rect.gameObject.AddComponent<RawImage>();
            sky.texture = CreateGradientTexture(new Color(0.03f, 0.06f, 0.2f), new Color(0.12f, 0.25f, 0.5f));
            rect.gameObject.AddComponent<PointerReceiver>().Pressed += OnSkyPressed;
        }

        private static Texture2D CreateGradientTexture(Color top, Color bottom)
        {
            Texture2D texture = new Texture2D(1, 256, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 256; y++)
            {
                texture.SetPixel(0, y, Color.Lerp(bottom, top, y / 255f));
            }

            texture.Apply(false, true);
            return texture;
        }

        private static void BuildSnowGround(Transform parent)
        {
            Image ground = PlaceImage(parent, "SnowGround", EffectShape.Dot, new Vector2(960f, -40f), new Vector2(2800f, 420f), new Color(0.93f, 0.96f, 1f));
            ground.raycastTarget = false;
        }

        private static void BuildSnowFall(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "SnowFall");
            rect.gameObject.AddComponent<SnowFall>().Build(70);
        }

        private static void BuildTree(Transform parent)
        {
            Color green = new Color(0.1f, 0.5f, 0.22f);
            Color darkGreen = new Color(0.06f, 0.38f, 0.16f);
            PlaceImage(parent, "Trunk", EffectShape.Bar, new Vector2(960f, 150f), new Vector2(120f, 150f), new Color(0.45f, 0.26f, 0.12f)).sprite = null;
            PlaceImage(parent, "TreeBottom", EffectShape.Triangle, new Vector2(960f, 400f), new Vector2(820f, 420f), darkGreen);
            PlaceImage(parent, "TreeMiddle", EffectShape.Triangle, new Vector2(960f, 590f), new Vector2(600f, 340f), green);
            PlaceImage(parent, "TreeTop", EffectShape.Triangle, new Vector2(960f, 765f), new Vector2(380f, 290f), green);
        }

        private static Image PlaceImage(Transform parent, string name, EffectShape shape, Vector2 center, Vector2 size, Color color)
        {
            RectTransform rect = UiFactory.CreateRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = EffectSprites.Get(shape);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image BuildStar(Transform parent)
        {
            return PlaceImage(parent, "Star", EffectShape.Star, new Vector2(ChristmasTreeLayout.CenterX, ChristmasTreeLayout.ApexY + 25f), new Vector2(160f, 160f), StarDimColor);
        }

        private static EffectSpawner BuildEffectLayer(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Effects");
            EffectSpawner effectSpawner = rect.gameObject.AddComponent<EffectSpawner>();
            GameEffectSettings settings = ScriptableObject.CreateInstance<GameEffectSettings>();
            settings.effectColor = new Color(1f, 0.85f, 0.3f, 1f);
            settings.effectScale = 1f;
            effectSpawner.Initialize(settings);
            return effectSpawner;
        }

        private void BuildKidsTexts(Transform parent)
        {
            instructionText = PlaceKidsText(parent, "ボールを かべに あてて ツリーを かざろう！", 58, Color.white, new Vector2(960f, 1015f), new Vector2(1800f, 90f));
            counterText = PlaceKidsText(parent, string.Empty, 110, GoldText, new Vector2(330f, 640f), new Vector2(560f, 300f));
            celebrationText = PlaceKidsText(parent, "クリスマスツリーの完成～！", 120, GoldText, new Vector2(960f, 520f), new Vector2(1900f, 200f));
            countdownTitleText = PlaceKidsText(parent, "つぎは おはなばたけ！", 80, Color.white, new Vector2(960f, 760f), new Vector2(1600f, 140f));
            countdownNumberText = PlaceKidsText(parent, string.Empty, 340, GoldText, new Vector2(960f, 440f), new Vector2(600f, 420f));
        }

        private static Text PlaceKidsText(Transform parent, string content, int fontSize, Color color, Vector2 center, Vector2 size)
        {
            Text text = UiFactory.CreateKidsText(parent, content, fontSize, color);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return text;
        }

        private void BuildOperatorOverlay(Transform parent)
        {
            overlay = UiFactory.CreateRect(parent, "Overlay（スペースで隠す）").gameObject;
            UiFactory.Stretch((RectTransform)overlay.transform);
            Image panel = UiFactory.CreatePanel(overlay.transform, "OperatorPanel", UiFactory.PanelColor);
            RectTransform rect = panel.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(20f, 20f);
            rect.sizeDelta = new Vector2(1100f, 130f);
            operatorText = UiFactory.CreateText(panel.transform, string.Empty, 24, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(operatorText.rectTransform, 16f, 10f, 820f, 110f);
            Button back = UiFactory.CreateButton(panel.transform, "校正へ戻る", 26, OnBackToCalibration);
            UiFactory.PlaceTopLeft((RectTransform)back.transform, 860f, 30f, 220f, 70f);
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                overlay.SetActive(!overlay.activeSelf);
            }
        }

        private void HandleHits()
        {
            foreach (Vector2 wallUv in WallHitInput.CollectHits(session, "Christmas"))
            {
                OnWallHit(wallUv);
            }
        }

        private void OnWallHit(Vector2 wallUv)
        {
            spawner.SpawnEffect(EffectKind.FlashParticles, wallUv);
            int ornamentIndex = flow.HitCount;
            if (flow.RegisterHit())
            {
                AddOrnament(ornamentIndex, wallUv);
            }
        }

        private void AddOrnament(int ornamentIndex, Vector2 wallUv)
        {
            Vector2 slot = slots[slotOrder[ornamentIndex % slotOrder.Length]];
            Image ornament = PlaceImage(ornamentLayer, "Ornament " + (ornamentIndex + 1), EffectShape.Dot, slot, new Vector2(84f, 84f), OrnamentColors[random.Next(OrnamentColors.Length)]);
            Image shine = PlaceImage(ornament.transform, "Shine", EffectShape.SoftCircle, Vector2.zero, new Vector2(34f, 34f), new Color(1f, 1f, 1f, 0.85f));
            RectTransform shineRect = shine.rectTransform;
            shineRect.anchorMin = new Vector2(0.5f, 0.5f);
            shineRect.anchorMax = new Vector2(0.5f, 0.5f);
            shineRect.anchoredPosition = new Vector2(-16f, 16f);
            Vector2 from = new Vector2(wallUv.x * ornamentLayer.rect.width, wallUv.y * ornamentLayer.rect.height);
            ornament.gameObject.AddComponent<OrnamentFlight>().Fly(from, slot);
        }

        private void AnimateCelebration()
        {
            bool celebrating = flow.Phase == ChristmasPhase.Celebrating || flow.Phase == ChristmasPhase.Countdown;
            star.color = celebrating ? StarBrightColor : StarDimColor;
            float pulse = celebrating ? 1f + 0.15f * Mathf.Sin(Time.time * 6f) : 1f;
            star.rectTransform.localScale = Vector3.one * pulse;
            if (flow.Phase == ChristmasPhase.Celebrating && Time.time >= nextBurstAt)
            {
                nextBurstAt = Time.time + CelebrationBurstInterval;
                SpawnCelebrationBurst();
            }
        }

        private void SpawnCelebrationBurst()
        {
            Vector2 wallUv = new Vector2(0.2f + 0.6f * (float)random.NextDouble(), 0.3f + 0.6f * (float)random.NextDouble());
            EffectKind kind = random.Next(2) == 0 ? EffectKind.FlashParticles : EffectKind.Petals;
            spawner.SpawnEffect(kind, wallUv);
        }

        private void RefreshTexts()
        {
            ChristmasPhase phase = flow.Phase;
            bool playing = phase == ChristmasPhase.Playing;
            instructionText.gameObject.SetActive(playing);
            counterText.gameObject.SetActive(playing);
            counterText.text = "あと\n" + flow.Remaining + "こ！";
            celebrationText.gameObject.SetActive(phase == ChristmasPhase.Celebrating);
            celebrationText.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 5f));
            bool countdown = phase == ChristmasPhase.Countdown;
            countdownTitleText.gameObject.SetActive(countdown);
            countdownNumberText.gameObject.SetActive(countdown);
            countdownNumberText.text = flow.GetCountdownNumber().ToString();
            countdownNumberText.rectTransform.localScale = Vector3.one * GetCountdownPop();
            operatorText.text = BuildOperatorText();
        }

        private float GetCountdownPop()
        {
            float withinSecond = flow.GetPhaseElapsed() % 1f;
            return Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(withinSecond * 4f));
        }

        private string BuildOperatorText()
        {
            DetectionResult result = session.LatestResult;
            string blob = result != null ? result.DescribeLargestBlob() : string.Empty;
            string state = result != null && result.PersonDetected ? "（人を検知中）" : string.Empty;
            string click = session.IsSimulated ? "\n模擬深度：画面をクリックするとボールが当たります" : string.Empty;
            return "クリスマス　飾り " + flow.HitCount + " / " + flow.Goal + state + "\n" + blob + click;
        }

        private void OnSkyPressed(Vector2 wallUv)
        {
            WallHitInput.InjectClick(session, wallUv);
        }

        private void OnBackToCalibration()
        {
            sceneRequested = true;
            SceneManager.LoadScene(CalibrationSceneName);
        }

        private void GoToNextSceneWhenFinished()
        {
            if (flow.Phase != ChristmasPhase.Finished || sceneRequested)
            {
                return;
            }

            sceneRequested = true;
            SceneManager.LoadScene(NextSceneName);
        }
    }
}
