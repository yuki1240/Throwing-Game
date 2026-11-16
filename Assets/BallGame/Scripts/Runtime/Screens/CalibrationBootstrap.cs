using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// Calibration シーンの起動用オブジェクト。実行時に 1920×1080 の Canvas で校正画面を組み立てます。
    /// 校正は現場の画面だけで完結します（設置の選択 → 四隅のタップ → 基準の録画 → ゲームへ）。
    /// </summary>
    public sealed class CalibrationBootstrap : MonoBehaviour
    {
        /// <summary>Game シーンの名前。</summary>
        public const string GameSceneName = "Game";

        /// <summary>リセットの確認を待つ秒数。</summary>
        public const float ResetConfirmSeconds = 3f;

        private const float PreviewLeft = 40f;
        private const float PreviewTop = 110f;
        private const float PreviewMaxWidth = 1100f;
        private const float PreviewMaxHeight = 930f;
        private const float PanelLeft = 1180f;
        private const float PanelWidth = 700f;

        private static readonly Color WaitingColor = new Color(0.25f, 0.25f, 0.3f, 1f);

        private static readonly string[] CornerNames = { "左上", "右上", "右下", "左下" };

        private CalibrationSession session;
        private readonly DepthPreviewRenderer previewRenderer = new DepthPreviewRenderer();
        private BaselineRecorder recorder;
        private RawImage preview;
        private RectTransform[] cornerMarkers;
        private Image[] cornerLines;
        private Button sideButton;
        private Button belowButton;
        private Button mirrorButton;
        private Button sourceButton;
        private Button recordButton;
        private Button resetButton;
        private Button gameButton;
        private Text distanceText;
        private Text guideText;
        private Text statusText;
        private float resetRequestedAt = float.NegativeInfinity;

        private void Start()
        {
            session = CalibrationSession.EnsureExists();
            EventSystemKeeper.Ensure();
            BuildScreen();
            RefreshAll();
        }

        private void Update()
        {
            session = CalibrationSession.EnsureExists();
            if (session.HasNewFrame)
            {
                DrawPreview();
                FeedRecorder();
            }

            RefreshAll();
        }

        private void OnDestroy()
        {
            previewRenderer.Dispose();
        }

        private void BuildScreen()
        {
            Canvas canvas = UiFactory.CreateCanvas("CalibrationCanvas", 0);
            Image background = UiFactory.CreatePanel(canvas.transform, "Background", new Color(0.93f, 0.93f, 0.93f, 1f));
            UiFactory.Stretch(background.rectTransform);
            Text title = UiFactory.CreateText(canvas.transform, "BallGame 校正", 44, TextAnchor.MiddleLeft);
            UiFactory.PlaceTopLeft(title.rectTransform, PreviewLeft, 24f, 1000f, 70f);
            BuildPreview(canvas.transform);
            BuildPanel(canvas.transform);
        }

        private void BuildPreview(Transform parent)
        {
            Image frame = UiFactory.CreatePanel(parent, "PreviewFrame", Color.black);
            UiFactory.PlaceTopLeft(frame.rectTransform, PreviewLeft, PreviewTop, PreviewMaxWidth, PreviewMaxHeight);
            RectTransform previewRect = UiFactory.CreateRect(frame.transform, "DepthPreview");
            preview = previewRect.gameObject.AddComponent<RawImage>();
            preview.color = WaitingColor;
            previewRect.gameObject.AddComponent<PointerReceiver>().Pressed += OnPreviewPressed;
            BuildCornerLines(previewRect);
            BuildCornerMarkers(previewRect);
        }

        private void BuildCornerMarkers(RectTransform parent)
        {
            cornerMarkers = new RectTransform[CalibrationData.CornerCount];
            for (int i = 0; i < cornerMarkers.Length; i++)
            {
                Image marker = UiFactory.CreatePanel(parent, "Corner " + CornerNames[i], new Color(0.2f, 0.9f, 0.3f, 1f));
                marker.raycastTarget = false;
                marker.rectTransform.sizeDelta = new Vector2(22f, 22f);
                SetTopLeftAnchor(marker.rectTransform, new Vector2(0.5f, 0.5f));
                Text label = UiFactory.CreateText(marker.transform, (i + 1) + " " + CornerNames[i], 26, TextAnchor.MiddleLeft);
                label.color = new Color(0.2f, 1f, 0.3f, 1f);
                UiFactory.PlaceTopLeft(label.rectTransform, 26f, -8f, 160f, 40f);
                cornerMarkers[i] = marker.rectTransform;
            }
        }

        private void BuildCornerLines(RectTransform parent)
        {
            cornerLines = new Image[CalibrationData.CornerCount];
            for (int i = 0; i < cornerLines.Length; i++)
            {
                Image line = UiFactory.CreatePanel(parent, "Line " + i, new Color(0.2f, 0.9f, 0.3f, 0.9f));
                line.raycastTarget = false;
                SetTopLeftAnchor(line.rectTransform, new Vector2(0f, 0.5f));
                cornerLines[i] = line;
            }
        }

        private static void SetTopLeftAnchor(RectTransform rect, Vector2 pivot)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = pivot;
        }

        private void BuildPanel(Transform parent)
        {
            Image panel = UiFactory.CreatePanel(parent, "Panel", UiFactory.PanelColor);
            UiFactory.PlaceTopLeft(panel.rectTransform, PanelLeft, PreviewTop, PanelWidth, PreviewMaxHeight);
            Transform p = panel.transform;
            AddLabel(p, "1. 設置を選ぶ", 20f);
            sideButton = AddButton(p, MountGeometry.GetDisplayName(MountOrientation.Side), 20f, 70f, 330f, () => OnMountPressed(MountOrientation.Side));
            belowButton = AddButton(p, MountGeometry.GetDisplayName(MountOrientation.Below), 350f, 70f, 330f, () => OnMountPressed(MountOrientation.Below));
            mirrorButton = AddButton(p, "左右反転", 20f, 150f, 330f, OnMirrorPressed);
            sourceButton = AddButton(p, "入力源", 350f, 150f, 330f, OnSourcePressed);
            AddLabel(p, "2. 左のプレビューで四隅をタップ", 230f);
            AddButton(p, "四隅をやり直す", 20f, 280f, 660f, OnClearCornersPressed);
            AddLabel(p, "3. 人がいない状態で基準を録る", 360f);
            recordButton = AddButton(p, "基準を録る（30フレーム）", 20f, 410f, 660f, OnRecordPressed);
            BuildDistanceRow(p, 490f);
            guideText = UiFactory.CreateText(p, string.Empty, 26, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(guideText.rectTransform, 20f, 560f, 660f, 170f);
            resetButton = AddButton(p, "リセット（すべて消す）", 20f, 740f, 320f, OnResetPressed);
            gameButton = AddButton(p, "4. ゲームへ", 360f, 740f, 320f, OnGamePressed);
            statusText = UiFactory.CreateText(p, string.Empty, 22, TextAnchor.UpperLeft);
            UiFactory.PlaceTopLeft(statusText.rectTransform, 20f, 830f, 660f, 90f);
        }

        private void BuildDistanceRow(Transform parent, float y)
        {
            Text label = UiFactory.CreateText(parent, "壁からの距離", 28, TextAnchor.MiddleLeft);
            UiFactory.PlaceTopLeft(label.rectTransform, 20f, y, 220f, 60f);
            AddButton(parent, "－", 250f, y, 90f, () => ChangeDistance(-1));
            distanceText = UiFactory.CreateText(parent, string.Empty, 30, TextAnchor.MiddleCenter);
            UiFactory.PlaceTopLeft(distanceText.rectTransform, 350f, y, 140f, 60f);
            AddButton(parent, "＋", 500f, y, 90f, () => ChangeDistance(1));
        }

        private static void AddLabel(Transform parent, string content, float y)
        {
            Text text = UiFactory.CreateText(parent, content, 28, TextAnchor.MiddleLeft);
            UiFactory.PlaceTopLeft(text.rectTransform, 20f, y, 660f, 44f);
        }

        private static Button AddButton(Transform parent, string label, float x, float y, float width, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiFactory.CreateButton(parent, label, 28, onClick);
            UiFactory.PlaceTopLeft((RectTransform)button.transform, x, y, width, 64f);
            return button;
        }

        private void OnPreviewPressed(Vector2 normalized)
        {
            Vector2 previewUv = new Vector2(normalized.x, 1f - normalized.y);
            Vector2 imageUv = MountGeometry.PreviewToImage(previewUv, session.Data.Mount);
            session.AddCorner(imageUv);
        }

        private void OnMountPressed(MountOrientation mount)
        {
            CancelRecording();
            session.SelectMount(mount);
        }

        private void OnMirrorPressed()
        {
            session.SetMirror(!session.Data.MirrorX);
        }

        private void OnSourcePressed()
        {
            CancelRecording();
            DepthSourceKind next = session.Data.InputSource == DepthSourceKind.Kinect ? DepthSourceKind.Simulated : DepthSourceKind.Kinect;
            session.SetInputSource(next);
        }

        private void OnClearCornersPressed()
        {
            session.ClearCorners();
        }

        private void OnRecordPressed()
        {
            recorder = new BaselineRecorder(session.FrameWidth, session.FrameHeight);
            recorder.Begin();
        }

        private void OnResetPressed()
        {
            if (Time.unscaledTime - resetRequestedAt > ResetConfirmSeconds)
            {
                resetRequestedAt = Time.unscaledTime;
                return;
            }

            resetRequestedAt = float.NegativeInfinity;
            CancelRecording();
            session.ResetAll();
        }

        private void OnGamePressed()
        {
            if (CanStartGame())
            {
                SceneManager.LoadScene(GameSceneName);
            }
        }

        private void ChangeDistance(int deltaCm)
        {
            session.SetMaxDistanceCm(session.Data.GetMaxDistanceCm() + deltaCm);
        }

        private void CancelRecording()
        {
            if (recorder != null)
            {
                recorder.Cancel();
                recorder = null;
            }
        }

        private void FeedRecorder()
        {
            if (recorder == null || !recorder.IsRecording)
            {
                return;
            }

            if (recorder.Width != session.FrameWidth || recorder.Height != session.FrameHeight)
            {
                CancelRecording();
                return;
            }

            recorder.AddFrame(session.LatestDepth);
            if (recorder.IsComplete)
            {
                session.SetBaseline(recorder.BuildMedian(), recorder.Width, recorder.Height);
            }
        }

        private bool CanStartGame()
        {
            return session.IsSavedCalibrationUsable || session.IsSimulated;
        }

        private void DrawPreview()
        {
            DetectionResult result = session.LatestResult;
            bool[] candidates = result != null && session.IsSavedCalibrationUsable ? result.CandidateMask : null;
            HitDetector detector = session.IsSavedCalibrationUsable ? session.Detector : null;
            preview.color = Color.white;
            preview.texture = previewRenderer.Render(session.LatestDepth, session.FrameWidth, session.FrameHeight, session.Data.Mount, candidates, detector);
        }

        private void RefreshAll()
        {
            if (preview == null)
            {
                return;
            }

            LayoutPreview();
            RefreshCorners();
            RefreshButtons();
            RefreshTexts();
        }

        private void LayoutPreview()
        {
            Vector2 size = GetPreviewSize();
            RectTransform rect = preview.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private Vector2 GetPreviewSize()
        {
            int width = Mathf.Max(1, session.FrameWidth);
            int height = Mathf.Max(1, session.FrameHeight);
            float previewWidth = PreviewMaxWidth;
            float previewHeight = previewWidth * height / width;
            if (previewHeight > PreviewMaxHeight)
            {
                previewHeight = PreviewMaxHeight;
                previewWidth = previewHeight * width / height;
            }

            return new Vector2(previewWidth, previewHeight);
        }

        private void RefreshCorners()
        {
            CalibrationData data = session.Data;
            Vector2 size = preview.rectTransform.rect.size;
            for (int i = 0; i < cornerMarkers.Length; i++)
            {
                bool placed = i < data.Corners.Count;
                cornerMarkers[i].gameObject.SetActive(placed);
                if (placed)
                {
                    cornerMarkers[i].anchoredPosition = ImageToPreviewLocal(data.Corners[i], data.Mount, size);
                }
            }

            RefreshLines(data, size);
        }

        private void RefreshLines(CalibrationData data, Vector2 size)
        {
            for (int i = 0; i < cornerLines.Length; i++)
            {
                int next = (i + 1) % CalibrationData.CornerCount;
                bool visible = next < data.Corners.Count && (next != 0 || data.HasAllCorners);
                cornerLines[i].gameObject.SetActive(visible);
                if (visible)
                {
                    PlaceLine(cornerLines[i].rectTransform, ImageToPreviewLocal(data.Corners[i], data.Mount, size), ImageToPreviewLocal(data.Corners[next], data.Mount, size));
                }
            }
        }

        private static void PlaceLine(RectTransform line, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            line.anchoredPosition = from;
            line.sizeDelta = new Vector2(delta.magnitude, 4f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static Vector2 ImageToPreviewLocal(Vector2 imageUv, MountOrientation mount, Vector2 size)
        {
            Vector2 previewUv = MountGeometry.ImageToPreview(imageUv, mount);
            return new Vector2(previewUv.x * size.x, -previewUv.y * size.y);
        }

        private void RefreshButtons()
        {
            CalibrationData data = session.Data;
            UiFactory.SetButtonColor(sideButton, data.Mount == MountOrientation.Side ? UiFactory.SelectedColor : UiFactory.ButtonColor);
            UiFactory.SetButtonColor(belowButton, data.Mount == MountOrientation.Below ? UiFactory.SelectedColor : UiFactory.ButtonColor);
            UiFactory.SetButtonLabel(mirrorButton, "左右反転：" + (data.MirrorX ? "オン" : "オフ"));
            UiFactory.SetButtonLabel(sourceButton, "入力源：" + (data.InputSource == DepthSourceKind.Kinect ? "Kinect" : "模擬"));
            UiFactory.SetButtonLabel(recordButton, GetRecordLabel());
            UiFactory.SetButtonLabel(resetButton, IsWaitingResetConfirm() ? "もう一度押すとリセット" : "リセット（すべて消す）");
            gameButton.interactable = CanStartGame();
            distanceText.text = data.GetMaxDistanceCm() + "cm";
        }

        private string GetRecordLabel()
        {
            if (recorder != null && recorder.IsRecording)
            {
                return "録画中… " + recorder.RecordedFrames + " / " + recorder.TargetFrames;
            }

            return session.IsSavedCalibrationUsable || session.Data.MatchesResolution(session.FrameWidth, session.FrameHeight)
                ? "基準を録り直す（30フレーム）"
                : "基準を録る（30フレーム）";
        }

        private bool IsWaitingResetConfirm()
        {
            return Time.unscaledTime - resetRequestedAt <= ResetConfirmSeconds;
        }

        private void RefreshTexts()
        {
            guideText.text = BuildGuide();
            statusText.text = session.SourceStatus + "\n" + BuildDetectionStatus();
        }

        private string BuildGuide()
        {
            CalibrationData data = session.Data;
            string nearSide = MountGeometry.IsNearAtImageBottom(data.Mount) ? "画像の下が近く、上ほど遠い" : "画像の上が近く、下ほど遠い（表示は上下を入れ替えています）";
            string step;
            if (!data.HasAllCorners)
            {
                step = "次：プレビューで「" + CornerNames[data.Corners.Count] + "」をタップ（左上→右上→右下→左下）";
            }
            else if (!data.MatchesResolution(session.FrameWidth, session.FrameHeight))
            {
                step = "次：壁の前に人がいない状態で「基準を録る」";
            }
            else
            {
                step = "校正済み。赤い点は検知の候補です。「ゲームへ」で始めます。";
            }

            return MountGeometry.GetDisplayName(data.Mount) + "：" + nearSide + "\n" + step;
        }

        private string BuildDetectionStatus()
        {
            DetectionResult result = session.LatestResult;
            if (!session.IsSavedCalibrationUsable)
            {
                return session.IsSimulated ? "未校正のため、Game では仮の校正（模擬用）を使います。" : "未校正です。";
            }

            if (result == null)
            {
                return string.Empty;
            }

            string person = result.PersonDetected ? "（人を検知中）" : result.InCooldown ? "（人が去ったあとの待ち時間）" : string.Empty;
            return "変化画素：" + result.ChangedPixelCount + person;
        }
    }
}
