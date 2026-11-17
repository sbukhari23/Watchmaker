using UnityEngine;
using UnityEngine.UI;
using Watchmaker.Core;

namespace Watchmaker.UI
{
    /// <summary>World-space instruction panel built entirely in code (legacy UI Text, built-in font; no asset dependencies).</summary>
    public sealed class InstructionPanel : MonoBehaviour
    {
        Text _title, _body, _footer;
        Image _bar;
        RectTransform _barRect;
        string _lastTitle, _lastBody;
        float _lastProgress = -1f;

        static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Font.CreateDynamicFontFromOSFont("Arial", 40);
        }

        void Awake()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1000f, 520f);
            rect.localScale = Vector3.one * 0.0005f;

            Font font = LoadFont();
            MakeImage("Background", canvasGo.transform, Vector2.zero, new Vector2(1000f, 520f), new Color(0.07f, 0.06f, 0.05f, 0.82f));
            MakeImage("Accent", canvasGo.transform, new Vector2(0f, 250f), new Vector2(1000f, 8f), new Color(0.79f, 0.64f, 0.36f, 1f));
            _title = MakeText("Title", canvasGo.transform, font, 62, FontStyle.Bold, new Vector2(0f, 175f), new Vector2(920f, 100f), new Color(0.96f, 0.86f, 0.62f));
            _body = MakeText("Body", canvasGo.transform, font, 44, FontStyle.Normal, new Vector2(0f, 35f), new Vector2(900f, 200f), new Color(0.93f, 0.93f, 0.93f));
            _footer = MakeText("Footer", canvasGo.transform, font, 32, FontStyle.Italic, new Vector2(0f, -190f), new Vector2(920f, 60f), new Color(0.65f, 0.70f, 0.75f));
            _footer.text = "Tip: poke the brass lever on the left to magnify.";

            MakeImage("BarBack", canvasGo.transform, new Vector2(0f, -120f), new Vector2(800f, 22f), new Color(1f, 1f, 1f, 0.15f));
            _bar = MakeImage("BarFill", canvasGo.transform, new Vector2(-400f, -120f), new Vector2(800f, 22f), new Color(0.23f, 0.82f, 0.44f, 1f));
            _barRect = _bar.rectTransform;
            _barRect.pivot = new Vector2(0f, 0.5f);
            _barRect.anchoredPosition = new Vector2(-400f, -120f);
        }

        static Image MakeImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
            return img;
        }

        static Text MakeText(string name, Transform parent, Font font, int size, FontStyle style, Vector2 pos, Vector2 box, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = box;
            return t;
        }

        void Update()
        {
            GameFlow f = GameFlow.Instance;
            if (f == null) return;
            if (f.Title != _lastTitle) { _lastTitle = f.Title; _title.text = f.Title; }
            if (f.Body != _lastBody) { _lastBody = f.Body; _body.text = f.Body; }
            float p = f.Progress01;
            if (!Mathf.Approximately(p, _lastProgress))
            {
                _lastProgress = p;
                _barRect.sizeDelta = new Vector2(800f * p, 22f);
            }
            _bar.enabled = f.Current != Stage.Welcome;
        }
    }
}
