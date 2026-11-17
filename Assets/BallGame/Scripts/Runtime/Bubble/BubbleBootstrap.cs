using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// Bubble シーンの起動用オブジェクト。空にシャボン玉がふわふわ浮いていて、ボールを当てると「パチン」と割れます。
    /// 割れた数を数え、目標の数まで割ったら「すごい！」と出して、10秒のカウントダウンのあと次の場面（ランダム）へ移ります。
    /// 目標に届かなくても、10分たつと次の場面へ移ります（最後の1分に当たったときは、さらに1分待ちます）。
    /// 遊ぶのは幼稚園の子どもなので、文字は大きく、ひらがなを多くしています。
    /// </summary>
    public sealed class BubbleBootstrap : MonoBehaviour
    {
        /// <summary>校正のシーン名。</summary>
        public const string CalibrationSceneName = "Calibration";

        /// <summary>割る目標の数。</summary>
        public const int PopGoal = 30;

        private static readonly Color GoldText = new Color(1f, 0.92f, 0.35f, 1f);
        private static readonly Color CountText = new Color(1f, 0.55f, 0.1f, 1f);

        private readonly System.Random random = new System.Random();
        private readonly ChristmasFlow flow = new ChristmasFlow(PopGoal);
        private readonly SceneTimer timer = new SceneTimer();
        private readonly List<Bubble> popped = new List<Bubble>();
        private readonly Dictionary<Bubble, RectTransform> views = new Dictionary<Bubble, RectTransform>();
        private readonly List<Bubble> staleViews = new List<Bubble>();
        private CalibrationSession session;
        private BubbleField field;
        private RectTransform bubbleLayer;
        private RectTransform popLayer;
        private EffectSpawner spawner;
        private AudioSource audioSource;
        private Text instructionText;
        private Text countText;
        private Text celebrationText;
        private Text countdownTitleText;
        private Text countdownNumberText;
        private Text operatorText;
        private GameObject overlay;
        private Button holdButton;
        private BallMarker markers;
        private bool markersShown;
        private float nextBurstAt;
        private string nextSceneName;
        private bool sceneRequested;

        private void Start()
        {
            nextSceneName = SceneRotation.PickNext(SceneRotation.BubbleScene);
            ApplyHoldMode();
            session = CalibrationSession.EnsureExists();
            EventSystemKeeper.Ensure();
            field = new BubbleField(UiFactory.ReferenceResolution.x, UiFactory.ReferenceResolution.y, random);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            BuildScreen();
            RefreshTexts();
        }

        private void Update()
        {
            session = CalibrationSession.EnsureExists();
            HandleKeyboard();
            HandleHits();
            UpdateBallMarkers();
            field.Tick(Time.deltaTime);
            SyncBubbleViews();
            flow.Tick(Time.deltaTime);
            TickTimer();
            AnimateCelebration();
            RefreshTexts();
            GoToNextSceneWhenFinished();
        }

        private void BuildScreen()
        {
            Canvas canvas = UiFactory.CreateCanvas("BubbleCanvas", 0);
            BuildSky(canvas.transform);
            BuildClickCatcher(canvas.transform);
            bubbleLayer = CreateFullLayer(canvas.transform, "Bubbles");
            popLayer = CreateFullLayer(canvas.transform, "Pops");
            spawner = BuildEffectLayer(canvas.transform);
            markers = BallMarker.Create(canvas.transform);
            BuildKidsTexts(canvas.transform);
            BuildOperatorOverlay(canvas.transform);
        }

        private static RectTransform CreateFullLayer(Transform parent, string name)
        {
            RectTransform rect = UiFactory.CreateRect(parent, name);
            UiFactory.Stretch(rect);
            return rect;
        }

        private static void BuildSky(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Sky");
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = BubbleSprites.GetSky();
            image.raycastTarget = false;
        }

        private void BuildClickCatcher(Transform parent)
        {
            // 模擬深度のときに画面のクリックをボールの当たりにするための、透明な受け口
            RectTransform rect = CreateFullLayer(parent, "ClickCatcher");
            Image catcher = rect.gameObject.AddComponent<Image>();
            catcher.color = new Color(1f, 1f, 1f, 0f);
            rect.gameObject.AddComponent<PointerReceiver>().Pressed += OnSkyPressed;
        }

        private static EffectSpawner BuildEffectLayer(Transform parent)
        {
            RectTransform rect = CreateFullLayer(parent, "Effects");
            EffectSpawner effectSpawner = rect.gameObject.AddComponent<EffectSpawner>();
            GameEffectSettings settings = ScriptableObject.CreateInstance<GameEffectSettings>();
            settings.effectColor = new Color(1f, 0.85f, 0.4f, 1f);
            settings.effectScale = 0.8f;
            effectSpawner.Initialize(settings);
            return effectSpawner;
        }

        private void BuildKidsTexts(Transform parent)
        {
            instructionText = UiFactory.PlaceKidsText(parent, "ボールを あてて シャボンだまを わろう！", 58, Color.white, new Vector2(960f, 1015f), new Vector2(1800f, 90f));
            countText = UiFactory.PlaceKidsText(parent, string.Empty, 90, CountText, new Vector2(400f, 900f), new Vector2(760f, 150f));
            celebrationText = UiFactory.PlaceKidsText(parent, PopGoal + "こ われたよ！ すごーい！", 110, CountText, new Vector2(960f, 520f), new Vector2(1900f, 200f));
            countdownTitleText = UiFactory.PlaceKidsText(parent, SceneRotation.GetKidsTitle(nextSceneName), 80, Color.white, new Vector2(960f, 760f), new Vector2(1600f, 140f));
            countdownNumberText = UiFactory.PlaceKidsText(parent, string.Empty, 340, GoldText, new Vector2(960f, 440f), new Vector2(600f, 420f));
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
            rect.sizeDelta = new Vector2(1420f, 250f);
            operatorText = UiFactory.CreateText(panel.transform, string.Empty, 24, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(operatorText.rectTransform, 16f, 10f, 820f, 230f);
            Button back = UiFactory.CreateButton(panel.transform, "校正へ戻る", 26, OnBackToCalibration);
            UiFactory.PlaceTopLeft((RectTransform)back.transform, 860f, 30f, 220f, 70f);
            holdButton = HoldButton.Create(panel.transform, 1110f, 30f, ToggleHold);
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                // 固定モード：いまの場面をずっと続ける（場面の切り替えとカウントダウンをしない）
                ToggleHold();
            }

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                overlay.SetActive(!overlay.activeSelf);
            }

            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                // テスト用：Kinect がなくても、画面のどこかにボールが当たったことにする
                OnWallHit(new Vector2(0.05f + 0.9f * (float)random.NextDouble(), 0.1f + 0.8f * (float)random.NextDouble()));
            }
        }

        private void ToggleHold()
        {
            SceneHold.Toggle();
            ApplyHoldMode();
            HoldButton.Refresh(holdButton);
        }

        private void ApplyHoldMode()
        {
            flow.SetGoal(SceneHold.GetGoal(SceneHold.EndlessGoal));
        }

        private void UpdateBallMarkers()
        {
            if (!SceneHold.IsHolding)
            {
                if (markersShown)
                {
                    markers.Clear();
                    markersShown = false;
                }

                return;
            }

            markersShown = true;
            foreach (Vector2 position in WallHitInput.CollectBallPositions(session))
            {
                markers.Mark(position);
            }
        }

        private void HandleHits()
        {
            foreach (Vector2 wallUv in WallHitInput.CollectHits(session, "Bubble"))
            {
                OnWallHit(wallUv);
            }
        }

        private void OnWallHit(Vector2 wallUv)
        {
            if (SceneHold.IsHolding)
            {
                // 固定モード：玉は割らず、エフェクトも出さず、ボールのいる場所だけ見せる
                markers.Mark(wallUv);
                return;
            }

            if (flow.Phase != ChristmasPhase.Playing)
            {
                return;
            }

            // 最後の1分に当たったときは、場面を替える時間が1分延びる
            timer.NotifyHit();
            Vector2 canvasPosition = ToCanvas(wallUv);
            int count = field.PopAt(canvasPosition, popped);
            if (count == 0)
            {
                spawner.SpawnEffect(EffectKind.Flash, wallUv);
                return;
            }

            PlayPops(true);
            if (field.PoppedCount >= PopGoal)
            {
                flow.Complete();
            }
        }

        private static Vector2 ToCanvas(Vector2 wallUv)
        {
            return new Vector2(wallUv.x * UiFactory.ReferenceResolution.x, wallUv.y * UiFactory.ReferenceResolution.y);
        }

        // popped に入っている玉の割れる演出と音を出す
        private void PlayPops(bool showLabel)
        {
            foreach (Bubble bubble in popped)
            {
                BubblePop.Spawn(popLayer, bubble, random, showLabel);
            }

            audioSource.pitch = 0.9f + 0.4f * (float)random.NextDouble();
            audioSource.PlayOneShot(BubbleSprites.GetPopClip(), 0.8f);
        }

        private void SyncBubbleViews()
        {
            foreach (Bubble bubble in field.Bubbles)
            {
                RectTransform view;
                if (!views.TryGetValue(bubble, out view))
                {
                    view = CreateBubbleView(bubble);
                    views[bubble] = view;
                }

                view.anchoredPosition = bubble.Position;
            }

            staleViews.Clear();
            foreach (Bubble bubble in views.Keys)
            {
                if (!ContainsBubble(bubble))
                {
                    staleViews.Add(bubble);
                }
            }

            foreach (Bubble bubble in staleViews)
            {
                Destroy(views[bubble].gameObject);
                views.Remove(bubble);
            }
        }

        private bool ContainsBubble(Bubble bubble)
        {
            foreach (Bubble candidate in field.Bubbles)
            {
                if (candidate == bubble)
                {
                    return true;
                }
            }

            return false;
        }

        private RectTransform CreateBubbleView(Bubble bubble)
        {
            RectTransform rect = UiFactory.CreateRect(bubbleLayer, "Bubble");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(bubble.Radius * 2f, bubble.Radius * 2f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = BubbleSprites.GetBubble();
            image.color = BubbleSprites.GetTint(bubble.ColorIndex);
            image.raycastTarget = false;
            return rect;
        }

        private void AnimateCelebration()
        {
            bool celebrating = flow.Phase == ChristmasPhase.Celebrating || flow.Phase == ChristmasPhase.Countdown;
            if (!celebrating || Time.time < nextBurstAt)
            {
                return;
            }

            // お祝いの間は、玉が自分でぽんぽん割れる（数には入れない）
            nextBurstAt = Time.time + 0.25f;
            Bubble bubble;
            if (field.PopRandomVisible(out bubble))
            {
                popped.Clear();
                popped.Add(bubble);
                PlayPops(false);
                spawner.SpawnEffect(EffectKind.FlashParticles, new Vector2(bubble.Position.x / UiFactory.ReferenceResolution.x, bubble.Position.y / UiFactory.ReferenceResolution.y));
            }
        }

        private void TickTimer()
        {
            // 遊んでいる間だけ時間を数える（お祝いとカウントダウンは別に進む）
            if (flow.Phase == ChristmasPhase.Playing && !SceneHold.IsHolding)
            {
                timer.Tick(Time.unscaledDeltaTime);
            }
        }

        private void RefreshTexts()
        {
            ChristmasPhase phase = flow.Phase;
            bool playing = phase == ChristmasPhase.Playing;
            instructionText.gameObject.SetActive(playing);
            countText.gameObject.SetActive(playing);
            countText.text = "わった かず　" + field.PoppedCount + " こ";
            celebrationText.gameObject.SetActive(phase == ChristmasPhase.Celebrating);
            celebrationText.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 5f));
            // 目標まで割ったあとのカウントダウン、または10分たつ前の最後の10秒のカウントダウン
            int countdownNumber = phase == ChristmasPhase.Countdown ? flow.GetCountdownNumber() : timer.GetCountdownNumber();
            countdownNumber = SceneHold.IsHolding ? 0 : countdownNumber;
            bool countdown = countdownNumber > 0;
            countdownTitleText.gameObject.SetActive(countdown);
            countdownNumberText.gameObject.SetActive(countdown);
            countdownNumberText.text = countdownNumber.ToString();
            countdownNumberText.rectTransform.localScale = Vector3.one * (phase == ChristmasPhase.Countdown ? GetCountdownPop() : GetTimerCountdownPop(countdownNumber));
            operatorText.text = BuildOperatorText();
            HoldButton.Refresh(holdButton);
        }

        private float GetTimerCountdownPop(int number)
        {
            float withinSecond = Mathf.Clamp01(number - timer.RemainingSeconds);
            return Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(withinSecond * 4f));
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
            string click = (session.IsSimulated ? "\n模擬深度：画面をクリックするとボールが当たります" : string.Empty) + "\nＴキー：テスト用に1球当たったことにする";
            return "シャボン玉割り　割れた数 " + field.PoppedCount + " / " + PopGoal + "　場面が替わるまで " + timer.FormatRemaining() + SceneHold.GetOperatorHint() + state + "\n" + blob + click;
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
            if (SceneHold.IsHolding)
            {
                return;
            }

            bool timeUp = flow.Phase == ChristmasPhase.Playing && timer.IsExpired;
            if ((flow.Phase != ChristmasPhase.Finished && !timeUp) || sceneRequested)
            {
                return;
            }

            sceneRequested = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
