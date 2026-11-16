using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BallGame
{
    /// <summary>
    /// 実行時に 1920×1080 の Canvas と uGUI 部品を組み立てるための道具。
    /// 位置は Canvas の左上を原点にした画素（下向きが正）で指定します。
    /// </summary>
    public static class UiFactory
    {
        /// <summary>基準の解像度。</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>文字の色（黒）。</summary>
        public static readonly Color TextColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        /// <summary>下地の色（白に近い色）。</summary>
        public static readonly Color PanelColor = new Color(0.97f, 0.97f, 0.97f, 0.92f);

        /// <summary>ボタンの色。</summary>
        public static readonly Color ButtonColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        /// <summary>選ばれているボタンの色（緑）。</summary>
        public static readonly Color SelectedColor = new Color(0.45f, 0.85f, 0.45f, 1f);

        private static Font cachedFont;

        /// <summary>
        /// 日本語を表示できるフォントを返します（OS のフォントを使い、無ければ標準フォント）。
        /// </summary>
        /// <returns>フォント</returns>
        public static Font GetFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Hiragino Sans", "Noto Sans CJK JP", "Arial" }, 32);
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return cachedFont;
        }

        /// <summary>
        /// 1920×1080 基準の Canvas を作ります。
        /// </summary>
        /// <param name="name">名前</param>
        /// <param name="sortingOrder">描画順</param>
        /// <returns>Canvas</returns>
        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// 空の RectTransform を作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="name">名前</param>
        /// <returns>RectTransform</returns>
        public static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        /// <summary>
        /// 親いっぱいに広げます。
        /// </summary>
        /// <param name="rect">対象</param>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 親の左上を原点に、位置と大きさを決めます。
        /// </summary>
        /// <param name="rect">対象</param>
        /// <param name="x">左からの距離</param>
        /// <param name="y">上からの距離</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        public static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// 下地のパネルを作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="name">名前</param>
        /// <param name="color">色</param>
        /// <returns>パネルの Image</returns>
        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// 文字を作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="content">文字</param>
        /// <param name="fontSize">大きさ</param>
        /// <param name="alignment">寄せ方</param>
        /// <returns>Text</returns>
        public static Text CreateText(Transform parent, string content, int fontSize, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(parent, "Text");
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = GetFont();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// ボタンを作ります。
        /// </summary>
        /// <param name="parent">親</param>
        /// <param name="label">表示する文字</param>
        /// <param name="fontSize">文字の大きさ</param>
        /// <param name="onClick">押したときの処理</param>
        /// <returns>Button</returns>
        public static Button CreateButton(Transform parent, string label, int fontSize, UnityAction onClick)
        {
            Image background = CreatePanel(parent, "Button " + label, ButtonColor);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);
            Text text = CreateText(background.transform, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        /// <summary>
        /// ボタンの文字を変えます。
        /// </summary>
        /// <param name="button">ボタン</param>
        /// <param name="label">文字</param>
        public static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        /// <summary>
        /// ボタンの色を変えます。
        /// </summary>
        /// <param name="button">ボタン</param>
        /// <param name="color">色</param>
        public static void SetButtonColor(Button button, Color color)
        {
            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = color;
            }
        }
    }
}
