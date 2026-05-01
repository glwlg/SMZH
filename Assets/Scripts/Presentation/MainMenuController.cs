using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XTD.Content;

namespace XTD.Presentation
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private ContentCatalog catalog;
        private static Font cachedFont;

        private void Start()
        {
            catalog ??= DemoContentFactory.CreateCatalog();
            BuildUi();
        }

        private void BuildUi()
        {
            var root = new GameObject("主菜单界面");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var title = CreateText("标题", root.transform, new Vector2(0.5f, 0.68f), 52);
            title.text = "X-TD 原型";

            var start = CreateButton("开始战斗原型", root.transform, new Vector2(0.5f, 0.45f));
            start.onClick.AddListener(() => SceneManager.LoadScene("BattlePrototype"));

            var quit = CreateButton("退出", root.transform, new Vector2(0.5f, 0.34f));
            quit.onClick.AddListener(Application.Quit);
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchor, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(800f, 100f);
            var text = go.AddComponent<Text>();
            text.font = DefaultFont();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static Button CreateButton(string label, Transform parent, Vector2 anchor)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 72f);
            go.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.28f, 0.95f);

            var text = CreateText("文字", go.transform, new Vector2(0.5f, 0.5f), 24);
            text.text = label;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            return go.GetComponent<Button>();
        }

        private static Font DefaultFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18);
            if (cachedFont == null)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont("SimHei", 18);
            }

            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return cachedFont;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            AddBestInputModule(eventSystem);
        }

        private static void AddBestInputModule(GameObject eventSystem)
        {
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                var module = eventSystem.AddComponent(inputSystemModule);
                inputSystemModule.GetMethod("AssignDefaultActions")?.Invoke(module, null);
                return;
            }

            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
