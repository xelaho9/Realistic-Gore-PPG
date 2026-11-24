using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Mod
{
    // F8 panel, made from the game's own prefabs
    public class GoreSettingsUI : MonoBehaviour
    {
        static GoreSettingsUI inst;
        static GameObject rowPrefab, radioPrefab, sliderPrefab, buttonPrefab;
        static TMP_FontAsset font;

        GameObject canvas;
        RectTransform content;
        ScrollRect scroll;
        readonly List<TextMeshProUGUI> tabLabels = new List<TextMeshProUGUI>();
        readonly List<System.Action> syncs = new List<System.Action>();
        readonly Dictionary<string, float> applied = new Dictionary<string, float>();
        bool open, blocking, dirty;
        int tab = -1;
        float saveIn;

        public static void Ensure()
        {
            GoreSettings.Init();
            GrabPrefabs();
            if (inst) return;
            // a recompile leaves the old panel alive
            var old = GameObject.Find("RealisticGore Settings");
            if (old) Destroy(old);
            var go = new GameObject("RealisticGore Settings");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<GoreSettingsUI>();
        }
        public static void Open() { Ensure(); inst.SetOpen(true); }

        public static bool GrabPrefabs()
        {
            if (!rowPrefab)
                foreach (var g in Resources.FindObjectsOfTypeAll<SettingsGeneratorBehaviour>())
                    if (g.SettingTemplatePrefab && g.ButtonSettingPrefab && g.SliderSettingPrefab)
                    {
                        rowPrefab = g.SettingTemplatePrefab; radioPrefab = g.ButtonSettingPrefab; sliderPrefab = g.SliderSettingPrefab;
                        font = rowPrefab.GetComponent<SettingTemplateBehaviour>().Label.font;
                        break;
                    }
            if (!buttonPrefab)
                foreach (var d in Resources.FindObjectsOfTypeAll<DialogBox>())
                    if (d.DialogButtonPrefab) { buttonPrefab = d.DialogButtonPrefab; break; }
            return rowPrefab && buttonPrefab;
        }

        void SetOpen(bool v)
        {
            if (open == v) return;
            if (v && !canvas && !Build()) return;
            open = v;
            canvas.SetActive(v);
            Block(v);
            if (v) Sync(); else Save();
        }
        void Close()
        {
            SetOpen(false);
            if (Pending().Count > 0) AskRestart();
        }

        void Block(bool v)
        {
            if (v == blocking || !Global.main) return;
            blocking = v;
            if (v) Global.main.AddUiBlocker(); else Global.main.RemoveUiBlocker();
        }
        void OnDestroy() { Block(false); }

        void Update()
        {
            if (dirty && (saveIn -= Time.unscaledDeltaTime) <= 0f) Save();
            if (DialogBox.IsAnyDialogboxOpen) return;
            if (Input.GetKeyDown(KeyCode.F8)) { if (open) Close(); else SetOpen(true); }
            else if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void Save() { if (dirty) { GoreSettings.Save(); dirty = false; } }
        void Changed() { GoreSettings.Version++; dirty = true; saveIn = 1f; }

        bool Build()
        {
            if (!GrabPrefabs()) { Debug.LogWarning("[RealisticGore] settings menu prefabs not found"); return false; }

            canvas = new GameObject("GoreSettingsCanvas");
            canvas.transform.SetParent(transform, false);
            var cv = canvas.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 32000;
            var sc = canvas.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;
            canvas.AddComponent<GraphicRaycaster>();

            Fill(Box(canvas.transform, "Dim", 0, 0, 0, 0), new Color(0f, 0f, 0f, 0.5f));
            var win = Box(canvas.transform, "Window", 80, 60, 80, 60);
            Fill(win, new Color(0f, 0f, 0f, 0.92f));
            win.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.2f);

            var title = Label(win, "Realistic Gore", 36, Color.white);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(30f, -24f); title.rectTransform.sizeDelta = new Vector2(900f, 50f);
            var hint = Label(win, "F8", 28, new Color(1f, 1f, 1f, 0.561f));
            hint.alignment = TextAlignmentOptions.Right;
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(1f, 1f);
            hint.rectTransform.anchoredPosition = new Vector2(-30f, -24f); hint.rectTransform.sizeDelta = new Vector2(200f, 50f);

            // tabs down the left, like the game's settings
            var tabs = Box(win, "Tabs", 30, 100, 0, 130);
            tabs.anchorMax = new Vector2(0f, 1f); tabs.offsetMax = new Vector2(280f, -100f);
            var vl = tabs.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 10; vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            for (int i = 0; i < GoreSettings.Sections.Length; i++)
            {
                int t = i;
                string s = GoreSettings.Sections[i];
                tabLabels.Add(Button(tabs, s.Substring(0, 1) + s.Substring(1).ToLower(), 0f, () => ShowTab(t)));
            }

            BuildScroll(win);

            var bar = Box(win, "Buttons", 300, 0, 30, 0);
            bar.anchorMax = new Vector2(1f, 0f); bar.offsetMin = new Vector2(300f, 30f); bar.offsetMax = new Vector2(-30f, 110f);
            var hl = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 15; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            Button(bar, "Reset tab", 240f, () => { GoreSettings.Reset(GoreSettings.Sections[tab]); Changed(); Sync(); });
            Button(bar, "Reset all", 240f, () => { GoreSettings.Reset(null); Changed(); Sync(); });
            Button(bar, "Restart map", 240f, AskRestart);
            var spacer = new GameObject("Space", typeof(RectTransform));
            spacer.transform.SetParent(bar, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Button(bar, "Close", 240f, Close);

            ShowTab(0);
            return true;
        }

        // same setup as the game's settings scroll view
        void BuildScroll(RectTransform win)
        {
            var area = Box(win, "Scroll view", 300, 100, 30, 130);
            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60f;

            var view = Box(area, "Viewport", 0, 0, 22, 0);
            Fill(view, new Color(0f, 0f, 0f, 0.149f));
            view.gameObject.AddComponent<RectMask2D>();

            content = Box(view, "Content", 0, 0, 0, 0);
            content.anchorMin = new Vector2(0f, 1f); content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = new Vector2(0f, 0f);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(0, 7, 0, 0); vl.spacing = 20;
            vl.childControlWidth = true; vl.childControlHeight = false; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.MinSize;

            var sbRt = Box(area, "Scrollbar Vertical", 0, 0, 0, 0);
            sbRt.anchorMin = new Vector2(1f, 0f); sbRt.pivot = new Vector2(1f, 0.5f); sbRt.sizeDelta = new Vector2(17f, 0f);
            Fill(sbRt, new Color(0f, 0f, 0f, 0.329f));
            var handle = Box(Box(sbRt, "Sliding Area", 0, 0, 0, 0), "Handle", 0, 0, 0, 0);
            var sb = sbRt.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle; sb.targetGraphic = Fill(handle, new Color(1f, 1f, 1f, 0.251f));
            sb.direction = Scrollbar.Direction.BottomToTop;

            scroll.viewport = view; scroll.content = content;
            scroll.verticalScrollbar = sb; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        void ShowTab(int t)
        {
            if (t == tab) return;
            tab = t;
            foreach (Transform ch in content) Destroy(ch.gameObject);
            syncs.Clear();
            for (int i = 0; i < tabLabels.Count; i++) tabLabels[i].color = i == t ? Color.green : Color.white;
            foreach (var en in GoreSettings.All)
                if (en.sec == GoreSettings.Sections[t]) AddRow(en);
            scroll.verticalNormalizedPosition = 1f;
            UISoundBehaviour.Refresh();
            Sync();
        }

        void AddRow(GoreSettings.Entry e)
        {
            var row = Instantiate(rowPrefab, content).GetComponent<SettingTemplateBehaviour>();
            row.name = e.label;
            row.SetTitle(e.label);
            string desc = e.tip.Length > 0 ? char.ToUpper(e.tip[0]) + e.tip.Substring(1) + ". " : "";
            desc += "Default " + Fmt(e, e.def) + ".";
            if (e.restart) desc += " Needs a map restart.";
            row.SetDescription(desc);
            var le = row.Description.GetComponent<LayoutElement>();
            if (le) le.preferredWidth = 560f;

            if (e.IsBool)
            {
                var on = Radio(row, "Enabled", () => { e.Set(1f); Changed(); Sync(); });
                var off = Radio(row, "Disabled", () => { e.Set(0f); Changed(); Sync(); });
                syncs.Add(() => { bool v = e.Get() > 0.5f; on.color = v ? Color.green : Color.white; off.color = v ? Color.white : Color.green; });
                return;
            }

            var go = Instantiate(sliderPrefab, row.ControlContainer);
            var sl = go.GetComponentInChildren<Slider>();
            var shown = go.GetComponentInChildren<TextMeshProUGUI>();
            sl.minValue = e.min; sl.maxValue = e.max; sl.wholeNumbers = e.IsInt;
            float step = e.IsInt || e.max - e.min > 100f ? 1f : e.max - e.min > 10f ? 0.5f : 0.05f;
            sl.onValueChanged.AddListener(v =>
            {
                v = Mathf.Round(v / step) * step;
                sl.SetValueWithoutNotify(v);
                shown.text = Fmt(e, v);
                if (Mathf.Abs(v - e.Get()) > 0.0001f) { e.Set(v); Changed(); }
            });
            syncs.Add(() => { sl.SetValueWithoutNotify(e.Get()); shown.text = Fmt(e, e.Get()); });
        }

        // game's radio buttons check against its own prefs, so colour them ourselves
        TextMeshProUGUI Radio(SettingTemplateBehaviour row, string text, System.Action click)
        {
            var b = Instantiate(radioPrefab, row.ControlContainer).GetComponent<SettingButtonBehaviour>();
            b.Label.text = text;
            b.Button.onClick.AddListener(() => click());
            return b.Label;
        }

        TextMeshProUGUI Button(Transform parent, string text, float width, System.Action click)
        {
            var go = Instantiate(buttonPrefab, parent);
            go.name = text;
            var le = go.GetComponent<LayoutElement>();
            if (!le) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 70f;
            if (width > 0f) le.preferredWidth = width;
            go.GetComponent<Button>().onClick.AddListener(() => click());
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.text = text;
            return label;
        }

        void Sync() { foreach (var s in syncs) s(); }

        // restart-only settings that differ from what this map was built with
        List<GoreSettings.Entry> Pending()
        {
            var l = new List<GoreSettings.Entry>();
            foreach (var e in GoreSettings.All)
            {
                if (!e.restart) continue;
                float a;
                if (!applied.TryGetValue(e.key, out a)) applied[e.key] = e.Get();
                else if (Mathf.Abs(a - e.Get()) > 0.0005f) l.Add(e);
            }
            return l;
        }
        void MarkApplied() { foreach (var e in GoreSettings.All) if (e.restart) applied[e.key] = e.Get(); }

        void AskRestart()
        {
            SetOpen(false);
            if (!DialogBoxManager.Main) { RestartMap(); return; }
            var l = Pending();
            string q = "Restart the map? Everything on it gets cleared.";
            if (l.Count > 0)
            {
                q = "";
                foreach (var e in l) q += e.label + " changed. ";
                q += "Bodies already out keep the old one until the map restarts. Restart now?";
            }
            DialogBoxManager.Dialog(q,
                new DialogButton("Restart", true, RestartMap),
                new DialogButton("Not now", true, MarkApplied));
        }

        void RestartMap()
        {
            Save();
            MarkApplied();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static RectTransform Box(Transform parent, string name, float l, float t, float r, float b)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }
        static Image Fill(RectTransform rt, Color c) { var im = rt.gameObject.AddComponent<Image>(); im.color = c; return im; }
        static TextMeshProUGUI Label(Transform parent, string text, float size, Color c)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font) t.font = font;
            t.fontSize = size; t.color = c; t.text = text; t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }

        public static string Fmt(GoreSettings.Entry en, float v)
        {
            if (en.IsBool) return v > 0.5f ? "on" : "off";
            if (en.unit == "%") return Mathf.RoundToInt(v * 100f) + "%";
            if (en.IsInt) return ((int)v).ToString();
            string n = en.max - en.min > 100f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            if (en.unit == "x") return "x" + n;
            return en.unit.Length > 0 ? n + " " + en.unit : n;
        }
    }
}
