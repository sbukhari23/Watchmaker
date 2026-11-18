using UnityEngine;
using UnityEngine.UI;

namespace Watchmaker.Core
{
    /// <summary>Guided how-to-play walkthrough shown on a world-space canvas in front of the player at start. Buttons are poked with a fingertip.</summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        static readonly string[] Titles = {
            "Welcome, watchmaker", "1. Pick up parts", "2. Place each part", "3. Tighten the screws", "4. Wind the crown", "Ready to begin?" };
        static readonly string[] Bodies = {
            "Rebuild this mechanical watch piece by piece.\nThis short guide takes under a minute.\nPoke the NEXT button with your index finger.",
            "Reach out and PINCH a part between your thumb and index finger.\nKeep pinching to carry it. Open your fingers to let go.",
            "A GREEN outline shows where the part belongs.\nRelease it over the outline and it snaps in.\nOrder: three wheels, the bridge, then two screws.",
            "PINCH the screwdriver on the right of the bench and touch its tip to a screw until it is tight.\nPoking a screw with a fingertip works too.",
            "Circle your fingertip around the brass crown to wind the watch and start it.\nTip: poke the brass lever on the left for a magnifying loupe.",
            "The panel above the bench always shows your current step and progress.\nPoke START to begin." };

        public static bool Showing { get; private set; }
        static bool _seen;

        Canvas _canvas;
        Text _title, _body, _dots;
        Button3D _back, _next, _skip;
        Text _nextLabel;
        int _page;
        GameObject _instructionPanel;
        bool _placed;

        sealed class Button3D { public RectTransform Rect; public Image Img; public PokeState Poke; public Color Base; public bool Pressed; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _seen = false; Showing = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_seen) return;
            new GameObject("TutorialGuide").AddComponent<TutorialGuide>();
        }

        public static void Dismiss()
        {
            if (!Showing) return;
            var g = Object.FindFirstObjectByType<TutorialGuide>();
            if (g != null) g.Close();
            else Showing = false;
        }

        static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Font.CreateDynamicFontFromOSFont("Arial", 40);
        }

        void Start()
        {
            Showing = true;
            _instructionPanel = GameObject.Find("InstructionPanel");
            if (_instructionPanel != null) _instructionPanel.SetActive(false);
            Build();
            Refresh();
        }

        void Build()
        {
            var go = new GameObject("Canvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 600f);
            rt.localScale = Vector3.one * 0.00055f;
            Font font = LoadFont();
            Img("Bg", go.transform, Vector2.zero, new Vector2(1000f, 600f), new Color(0.07f, 0.06f, 0.05f, 0.92f));
            Img("Accent", go.transform, new Vector2(0f, 290f), new Vector2(1000f, 10f), new Color(0.79f, 0.64f, 0.36f, 1f));
            _title = Txt("Title", go.transform, font, 62, FontStyle.Bold, new Vector2(0f, 215f), new Vector2(920f, 100f), new Color(0.96f, 0.86f, 0.62f));
            _body = Txt("Body", go.transform, font, 40, FontStyle.Normal, new Vector2(0f, 45f), new Vector2(900f, 300f), new Color(0.93f, 0.93f, 0.93f));
            _dots = Txt("Dots", go.transform, font, 44, FontStyle.Bold, new Vector2(0f, -140f), new Vector2(600f, 60f), new Color(0.79f, 0.64f, 0.36f));
            _back = Btn("Back", go.transform, font, "BACK", new Vector2(-330f, -230f), null);
            _skip = Btn("Skip", go.transform, font, "SKIP", new Vector2(0f, -230f), null);
            _next = Btn("Next", go.transform, font, "NEXT", new Vector2(330f, -230f), t => _nextLabel = t);
            // always draw on top so the bench or parts can never hide the guide
            var onTop = new Material(Shader.Find("UI/Default"));
            onTop.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            foreach (Graphic g in go.GetComponentsInChildren<Graphic>(true)) g.material = onTop;
        }

        static Image Img(string n, Transform p, Vector2 pos, Vector2 size, Color c)
        {
            var g = new GameObject(n); g.transform.SetParent(p, false);
            var i = g.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
            i.rectTransform.anchoredPosition = pos; i.rectTransform.sizeDelta = size;
            return i;
        }

        static Text Txt(string n, Transform p, Font f, int size, FontStyle st, Vector2 pos, Vector2 box, Color c)
        {
            var g = new GameObject(n); g.transform.SetParent(p, false);
            var t = g.AddComponent<Text>(); t.font = f; t.fontSize = size; t.fontStyle = st; t.color = c;
            t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.rectTransform.anchoredPosition = pos; t.rectTransform.sizeDelta = box;
            return t;
        }

        Button3D Btn(string n, Transform p, Font f, string label, Vector2 pos, System.Action<Text> got)
        {
            Color c = new Color(0.79f, 0.64f, 0.36f, 1f);
            Image img = Img(n, p, pos, new Vector2(260f, 90f), c);
            Text t = Txt(n + "Label", img.transform, f, 44, FontStyle.Bold, Vector2.zero, new Vector2(260f, 90f), new Color(0.1f, 0.08f, 0.05f));
            t.text = label;
            got?.Invoke(t);
            return new Button3D { Rect = img.rectTransform, Img = img, Base = c };
        }

        void Refresh()
        {
            _title.text = Titles[_page];
            _body.text = Bodies[_page];
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Titles.Length; i++) sb.Append(i == _page ? "● " : "○ ");
            _dots.text = sb.ToString().Trim();
            _nextLabel.text = _page == Titles.Length - 1 ? "START" : "NEXT";
            _back.Img.enabled = _page > 0; _back.Rect.GetComponentInChildren<Text>().enabled = _page > 0;
        }

        bool Pressed(Button3D b)
        {
            if (!b.Img.enabled) return false;
            bool fired = b.Poke.Update(b.Rect.position, 0.03f, 0.6f);
            float d = HandTracker.NearestDistance(b.Rect.position);
            b.Img.color = d <= 0.05f ? Color.Lerp(b.Base, Color.white, 0.5f) : b.Base;
            return fired;
        }

        void Update()
        {
            if (!Showing) return;
            Camera cam = Camera.main;
            if (!_placed && cam != null && Time.time > 0.8f)
            {
                Vector3 f = cam.transform.forward; f.y = 0f;
                if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
                Vector3 pos = cam.transform.position + f.normalized * 0.55f + Vector3.up * 0.04f;
                transform.position = pos;
                transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
                _placed = true;
            }
            if (!_placed) return;
            if (Pressed(_next)) { if (_page >= Titles.Length - 1) Close(); else { _page++; Refresh(); } }
            if (Pressed(_back) && _page > 0) { _page--; Refresh(); }
            if (Pressed(_skip)) Close();
        }

        void Close()
        {
            Showing = false; _seen = true;
            if (_instructionPanel != null) _instructionPanel.SetActive(true);
            Destroy(gameObject);
        }

        void OnDestroy() { Showing = false; if (_instructionPanel != null) _instructionPanel.SetActive(true); }
    }
}
