using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XTD.Battle;
using XTD.Content;

namespace XTD.Presentation
{
    public sealed class BattleUiController : MonoBehaviour
    {
        private const float CardWidth = 112f;
        private const float CardHeight = 168f;

        private readonly List<CardView> cardViews = new();
        private BattleController battle;
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform handRoot;
        private RectTransform dragLayer;
        private Text statusText;
        private Text noticeText;
        private Text resultText;
        private Button restartButton;
        private float noticeTimer;
        private static Font cachedFont;

        public RectTransform CanvasRect => canvasRect;
        public RectTransform HandRoot => handRoot;
        public RectTransform DragLayer => dragLayer;

        public static BattleUiController CreateDefault()
        {
            var root = new GameObject("战斗界面");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var controller = root.AddComponent<BattleUiController>();
            controller.canvas = canvas;
            controller.canvasRect = root.GetComponent<RectTransform>();
            controller.BuildDefaultLayout(root.transform);
            return controller;
        }

        public void Bind(BattleController controller)
        {
            battle = controller;
            Refresh();
        }

        public void Refresh()
        {
            if (battle == null || statusText == null)
            {
                return;
            }

            var deck = battle.Deck;
            var cardPoolCount = deck != null ? deck.CardPool.Count : 0;
            var usedCount = deck != null ? deck.UsedPile.Count : 0;
            statusText.text =
                $"费用 {battle.Mana:0.0}/{battle.MaxMana}    统率 {battle.CurrentCommand}/{battle.MaxCommand}    士气 {battle.MoraleCharges}    卡池 {cardPoolCount} / 已用 {usedCount}\n" +
                $"我方基地 {battle.PlayerBaseHp:0}    敌方基地 {battle.EnemyBaseHp:0}";

            TickNotice();
            RenderHand();
        }

        public void ShowResult(string text)
        {
            if (resultText == null)
            {
                return;
            }

            resultText.text = text;
            resultText.gameObject.SetActive(true);
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(() => battle.StartPrototypeBattle());
            }
        }

        public void HideResult()
        {
            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }

            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(false);
            }

            ShowNotice(string.Empty, 0f);
        }

        public void ShowNotice(string text, float duration = 1.35f)
        {
            if (noticeText == null)
            {
                return;
            }

            noticeText.text = text;
            noticeText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
            noticeTimer = string.IsNullOrWhiteSpace(text) ? 0f : duration;
        }

        public Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return Vector3.zero;
            }

            var distance = Mathf.Abs(mainCamera.transform.position.z);
            var world = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            world.z = 0f;
            return world;
        }

        private void BuildDefaultLayout(Transform root)
        {
            var topPanel = CreatePanel(
                "顶部信息栏",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -46f),
                new Vector2(900f, 72f),
                new Color(0.025f, 0.035f, 0.045f, 0.68f));
            statusText = CreateText("状态", topPanel.transform, Vector2.zero, Vector2.one, Vector2.zero, 20);

            noticeText = CreateText("提示", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 204f), 20);
            noticeText.color = new Color(1f, 0.86f, 0.36f, 0.95f);
            noticeText.gameObject.SetActive(false);

            resultText = CreateText("战斗结果", root, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, 64);
            resultText.color = new Color(1f, 0.92f, 0.35f);
            resultText.gameObject.SetActive(false);

            restartButton = CreateButton("重新开始", root, new Vector2(0.5f, 0.46f), new Vector2(220f, 64f));
            restartButton.gameObject.SetActive(false);

            var hand = new GameObject("手牌区", typeof(RectTransform));
            hand.transform.SetParent(root, false);
            handRoot = hand.GetComponent<RectTransform>();
            handRoot.anchorMin = new Vector2(0.5f, 0f);
            handRoot.anchorMax = new Vector2(0.5f, 0f);
            handRoot.pivot = new Vector2(0.5f, 0f);
            handRoot.sizeDelta = new Vector2(820f, 210f);
            handRoot.anchoredPosition = new Vector2(0f, 8f);

            var drag = new GameObject("拖拽层", typeof(RectTransform));
            drag.transform.SetParent(root, false);
            dragLayer = drag.GetComponent<RectTransform>();
            dragLayer.anchorMin = Vector2.zero;
            dragLayer.anchorMax = Vector2.one;
            dragLayer.pivot = new Vector2(0.5f, 0.5f);
            dragLayer.offsetMin = Vector2.zero;
            dragLayer.offsetMax = Vector2.zero;
        }

        private void TickNotice()
        {
            if (noticeTimer <= 0f)
            {
                return;
            }

            noticeTimer -= Time.deltaTime;
            if (noticeTimer <= 0f && noticeText != null)
            {
                noticeText.gameObject.SetActive(false);
            }
        }

        private void RenderHand()
        {
            if (handRoot == null || battle.Deck == null)
            {
                return;
            }

            var hand = battle.Deck.Hand;
            while (cardViews.Count < hand.Count)
            {
                cardViews.Add(CardView.Create(this, handRoot, DefaultFont()));
            }

            for (var i = 0; i < cardViews.Count; i++)
            {
                var active = i < hand.Count;
                cardViews[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var card = hand[i];
                cardViews[i].Bind(this, battle, card, i, hand.Count, battle.CanPlayCard(card));
            }
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, int size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(860f, 86f);
            rect.anchoredPosition = position;
            var text = go.AddComponent<Text>();
            text.font = DefaultFont();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = size;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button CreateButton(string label, Transform parent, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.32f, 0.96f);

            var text = CreateText("文字", go.transform, Vector2.zero, Vector2.one, Vector2.zero, 24);
            text.rectTransform.sizeDelta = Vector2.zero;
            text.text = label;
            return go.GetComponent<Button>();
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }

            EnsureUsableInputModule(eventSystem.gameObject);
        }

        private static void EnsureUsableInputModule(GameObject eventSystem)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                foreach (var inputModule in eventSystem.GetComponents<BaseInputModule>())
                {
                    inputModule.enabled = inputModule.GetType() == inputSystemModule;
                }

                var module = eventSystem.GetComponent(inputSystemModule) ?? eventSystem.AddComponent(inputSystemModule);
                module.GetType().GetMethod("AssignDefaultActions")?.Invoke(module, null);
                return;
            }
#endif

            foreach (var inputModule in eventSystem.GetComponents<BaseInputModule>())
            {
                if (inputModule is not StandaloneInputModule)
                {
                    inputModule.enabled = false;
                }
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>() ?? eventSystem.AddComponent<StandaloneInputModule>();
            standalone.enabled = true;
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

        private sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private BattleUiController owner;
            private BattleController battle;
            private CardDefinition card;
            private RectTransform rect;
            private CanvasGroup canvasGroup;
            private Image frame;
            private Image innerPanel;
            private Image artFrameImage;
            private Image icon;
            private Image typeBand;
            private Image costBgImage;
            private Text title;
            private Text cost;
            private Text description;
            private bool hovered;
            private bool dragging;
            private bool canPlay;
            private Vector2 homePosition;
            private float homeRotation;
            private static Sprite cachedCardFrameSprite;

            public static CardView Create(BattleUiController owner, Transform parent, Font font)
            {
                var go = new GameObject("手牌", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(CardView));
                go.transform.SetParent(parent, false);

                var view = go.GetComponent<CardView>();
                view.owner = owner;
                view.rect = go.GetComponent<RectTransform>();
                view.rect.sizeDelta = new Vector2(CardWidth, CardHeight);
                view.rect.pivot = new Vector2(0.5f, 0f);

                view.canvasGroup = go.GetComponent<CanvasGroup>();
                view.frame = go.GetComponent<Image>();
                view.frame.sprite = CardFrameSprite();
                view.frame.type = Image.Type.Simple;
                view.frame.preserveAspect = false;
                view.frame.color = view.frame.sprite != null ? Color.white : new Color(0.20f, 0.10f, 0.08f, 0.97f);
                view.frame.raycastTarget = true;

                view.BuildVisuals(font);
                return view;
            }

            public void Bind(BattleUiController ownerController, BattleController battleController, CardDefinition definition, int index, int count, bool playable)
            {
                owner = ownerController;
                battle = battleController;
                card = definition;
                canPlay = playable;

                title.text = definition.displayName;
                cost.text = definition.cost.ToString();
                description.text = CardTypeLabel(definition);
                icon.sprite = definition.art;
                icon.enabled = definition.art != null;
                icon.preserveAspect = true;

                var playableColor = CardColor(definition.type);
                frame.color = frame.sprite != null
                    ? (playable ? Color.white : new Color(0.48f, 0.48f, 0.50f, 0.82f))
                    : (playable ? playableColor : new Color(0.07f, 0.07f, 0.08f, 0.82f));
                if (innerPanel != null)
                {
                    innerPanel.color = playable ? WithAlpha(playableColor, 0.64f) : new Color(0.07f, 0.07f, 0.08f, 0.68f);
                }

                if (artFrameImage != null)
                {
                    artFrameImage.color = playable ? new Color(0.07f, 0.05f, 0.04f, 0.58f) : new Color(0.05f, 0.05f, 0.06f, 0.66f);
                }

                if (typeBand != null)
                {
                    typeBand.color = playable ? WithAlpha(playableColor, 0.78f) : new Color(0.06f, 0.06f, 0.07f, 0.72f);
                }

                if (costBgImage != null)
                {
                    costBgImage.color = playable ? new Color(0.09f, 0.04f, 0.02f, 0.52f) : new Color(0.04f, 0.04f, 0.05f, 0.58f);
                }

                canvasGroup.alpha = playable ? 1f : 0.58f;

                SetHome(index, count);
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                hovered = true;
                ApplyHomeTransform();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                hovered = false;
                ApplyHomeTransform();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (card == null || battle == null || !canPlay)
                {
                    owner.ShowNotice("费用或统率不足");
                    return;
                }

                dragging = true;
                hovered = false;
                canvasGroup.alpha = 0.92f;
                canvasGroup.blocksRaycasts = false;
                rect.SetParent(owner.DragLayer, true);
                rect.SetAsLastSibling();
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * 1.08f;
                MoveToPointer(eventData.position);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (!dragging)
                {
                    return;
                }

                MoveToPointer(eventData.position);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (!dragging)
                {
                    return;
                }

                dragging = false;
                canvasGroup.blocksRaycasts = true;
                rect.SetParent(owner.HandRoot, false);

                var targetPosition = owner.ScreenToWorld(eventData.position);
                var played = battle.TryPlayCard(card, targetPosition);
                if (!played)
                {
                    if (!battle.CanReleaseCardAt(card, targetPosition, out var reason) || string.IsNullOrWhiteSpace(reason))
                    {
                        reason = "这里不能释放";
                    }

                    owner.ShowNotice(reason);
                }

                owner.Refresh();
            }

            private void BuildVisuals(Font font)
            {
                var inner = new GameObject("Card Interior", typeof(RectTransform), typeof(Image));
                inner.transform.SetParent(transform, false);
                var innerRect = inner.GetComponent<RectTransform>();
                innerRect.anchorMin = new Vector2(0.12f, 0.08f);
                innerRect.anchorMax = new Vector2(0.88f, 0.79f);
                innerRect.offsetMin = Vector2.zero;
                innerRect.offsetMax = Vector2.zero;
                innerPanel = inner.GetComponent<Image>();
                innerPanel.color = new Color(0.16f, 0.08f, 0.05f, 0.64f);
                innerPanel.raycastTarget = false;

                var artFrame = new GameObject("卡图框", typeof(RectTransform), typeof(Image));
                artFrame.transform.SetParent(transform, false);
                var artFrameRect = artFrame.GetComponent<RectTransform>();
                artFrameRect.anchorMin = new Vector2(0.16f, 0.42f);
                artFrameRect.anchorMax = new Vector2(0.84f, 0.76f);
                artFrameRect.offsetMin = Vector2.zero;
                artFrameRect.offsetMax = Vector2.zero;
                artFrameImage = artFrame.GetComponent<Image>();
                artFrameImage.color = new Color(0.08f, 0.06f, 0.05f, 0.58f);
                artFrameImage.raycastTarget = false;

                var iconGo = new GameObject("图标", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(artFrame.transform, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.08f, 0.08f);
                iconRect.anchorMax = new Vector2(0.92f, 0.92f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                icon = iconGo.GetComponent<Image>();
                icon.color = Color.white;
                icon.raycastTarget = false;

                var typeBandGo = new GameObject("Card Type Band", typeof(RectTransform), typeof(Image));
                typeBandGo.transform.SetParent(transform, false);
                var typeBandRect = typeBandGo.GetComponent<RectTransform>();
                typeBandRect.anchorMin = new Vector2(0.14f, 0.08f);
                typeBandRect.anchorMax = new Vector2(0.86f, 0.21f);
                typeBandRect.offsetMin = Vector2.zero;
                typeBandRect.offsetMax = Vector2.zero;
                typeBand = typeBandGo.GetComponent<Image>();
                typeBand.color = new Color(0.14f, 0.08f, 0.05f, 0.78f);
                typeBand.raycastTarget = false;

                title = CreateCardText("标题", transform, font, new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.40f), 17, TextAnchor.MiddleCenter);
                description = CreateCardText("类型", transform, font, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.22f), 12, TextAnchor.UpperCenter);

                var costBg = new GameObject("费用底", typeof(RectTransform), typeof(Image));
                costBg.transform.SetParent(transform, false);
                var costRect = costBg.GetComponent<RectTransform>();
                costRect.anchorMin = new Vector2(0.145f, 0.815f);
                costRect.anchorMax = new Vector2(0.145f, 0.815f);
                costRect.pivot = new Vector2(0.5f, 0.5f);
                costRect.anchoredPosition = Vector2.zero;
                costRect.sizeDelta = new Vector2(32f, 32f);
                costBgImage = costBg.GetComponent<Image>();
                costBgImage.color = new Color(0.09f, 0.04f, 0.02f, 0.52f);
                costBgImage.raycastTarget = false;

                cost = CreateCardText("费用", costBg.transform, font, Vector2.zero, Vector2.one, 20, TextAnchor.MiddleCenter);
                cost.color = new Color(1f, 0.86f, 0.42f);
                cost.rectTransform.sizeDelta = Vector2.zero;
                var outline = cost.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.04f, 0.015f, 0f, 0.95f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);
            }

            private void SetHome(int index, int count)
            {
                var center = (count - 1) * 0.5f;
                var offset = index - center;
                var normalized = count <= 1 ? 0f : offset / center;

                homePosition = new Vector2(offset * 82f, Mathf.Abs(normalized) * -18f);
                homeRotation = -normalized * 13f;
                ApplyHomeTransform();
            }

            private void ApplyHomeTransform()
            {
                if (dragging || rect == null)
                {
                    return;
                }

                if (rect.parent != owner.HandRoot)
                {
                    rect.SetParent(owner.HandRoot, false);
                }

                var lift = hovered && canPlay ? 38f : 0f;
                rect.anchoredPosition = homePosition + new Vector2(0f, lift);
                rect.localRotation = Quaternion.Euler(0f, 0f, hovered ? homeRotation * 0.45f : homeRotation);
                rect.localScale = Vector3.one * (hovered && canPlay ? 1.1f : 1f);
                rect.SetSiblingIndex(transform.GetSiblingIndex());
                canvasGroup.alpha = canPlay ? 1f : 0.58f;
            }

            private void MoveToPointer(Vector2 screenPosition)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.DragLayer, screenPosition, null, out var localPoint))
                {
                    rect.anchoredPosition = localPoint - new Vector2(0f, CardHeight * 0.2f);
                }
            }

            private static Text CreateCardText(string name, Transform parent, Font font, Vector2 anchorMin, Vector2 anchorMax, int size, TextAnchor alignment)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var text = go.AddComponent<Text>();
                text.font = font;
                text.fontSize = size;
                text.alignment = alignment;
                text.color = Color.white;
                text.raycastTarget = false;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 9;
                text.resizeTextMaxSize = size;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                return text;
            }

            private static string CardTypeLabel(CardDefinition definition)
            {
                return definition.type switch
                {
                    CardType.Structure => "建筑",
                    CardType.Spell => "法术",
                    CardType.Tactic => "战术",
                    CardType.EliteSoldier => "精兵",
                    CardType.Hero => "英雄",
                    CardType.Soldier => "召唤",
                    _ => "卡牌"
                };
            }

            private static Color CardColor(CardType type)
            {
                return type switch
                {
                    CardType.Structure => new Color(0.35f, 0.18f, 0.10f, 0.97f),
                    CardType.Spell => new Color(0.30f, 0.09f, 0.10f, 0.97f),
                    CardType.Tactic => new Color(0.16f, 0.20f, 0.12f, 0.97f),
                    CardType.EliteSoldier or CardType.Hero => new Color(0.20f, 0.16f, 0.08f, 0.97f),
                    _ => new Color(0.12f, 0.16f, 0.18f, 0.97f)
                };
            }

            private static Color WithAlpha(Color color, float alpha)
            {
                color.a = alpha;
                return color;
            }

            private static Sprite CardFrameSprite()
            {
                if (cachedCardFrameSprite != null)
                {
                    return cachedCardFrameSprite;
                }

                cachedCardFrameSprite = Resources.Load<Sprite>("UI/card_frame_honghuang");
                if (cachedCardFrameSprite != null)
                {
                    return cachedCardFrameSprite;
                }

                var texture = Resources.Load<Texture2D>("UI/card_frame_honghuang");
                if (texture == null)
                {
                    return null;
                }

                cachedCardFrameSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                return cachedCardFrameSprite;
            }
        }
    }
}
