using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XTD.Content;
using XTD.Flow;
using XTD.Roguelike;

namespace XTD.Presentation
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private ContentCatalog catalog;
        private static Font cachedFont;
        private static readonly Dictionary<string, Sprite> cachedResourceSprites = new();
        private static Sprite cachedCircleSprite;
        private GameFlowController flow;
        private GameObject uiRoot;
        private SidePanelMode sidePanelMode = SidePanelMode.None;
        private TitlePanelMode titlePanelMode = TitlePanelMode.Start;
        private HeroClassType titlePreviewHeroClass = GameContentFactory.DefaultHeroClass;
        private bool titleShowFullCardPool;

        private enum TitlePanelMode
        {
            Start,
            HeroClassSelect
        }

        private enum SidePanelMode
        {
            None,
            Deck,
            Artifacts,
            Progress,
            Codex,
            Log
        }

        private void Start()
        {
            catalog ??= GameContentFactory.CreateCatalog();
            GameContentFactory.EnsureCatalogComplete(catalog);
            flow = GameFlowController.EnsureInstance();
            flow.ConfigureCatalog(catalog);
            BuildUi();
        }

        private void BuildUi()
        {
            if (uiRoot != null)
            {
                Destroy(uiRoot);
            }

            uiRoot = new GameObject("主菜单界面");
            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            uiRoot.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            BuildBackdrop(uiRoot.transform);

            if (flow.CurrentRun != null && flow.CurrentRun.isComplete)
            {
                BuildRunEnd("探索完成", "最终首领已被击败，本次迷宫探索结束。");
                return;
            }

            if (flow.CurrentRun != null && flow.CurrentRun.isDefeated)
            {
                BuildRunEnd("探索失败", "主角生命归零，本次迷宫探索结束。");
                return;
            }

            if (!flow.HasActiveRun)
            {
                BuildTitleMenu();
                return;
            }

            if (flow.HasPendingCardReward)
            {
                BuildCardRewardPanel();
                return;
            }

            if (flow.HasPendingNode)
            {
                BuildNodePanel(flow.PendingNode);
                return;
            }

            BuildMapPanel();
        }

        private void BuildTitleMenu()
        {
            var selectingClass = titlePanelMode == TitlePanelMode.HeroClassSelect;
            if (!CreateTitleLogo(selectingClass))
            {
                var title = CreateText("标题", uiRoot.transform, selectingClass ? new Vector2(0.5f, 0.90f) : new Vector2(0.5f, 0.83f), new Vector2(860f, 110f), selectingClass ? 64 : 56, TextAnchor.MiddleCenter);
                title.text = "神魔镇荒";
                title.color = selectingClass ? new Color(0.92f, 0.96f, 0.91f, 0.98f) : new Color(0.86f, 1f, 0.96f, 0.98f);
                AddTextShadow(title, new Color(0.16f, 0.0f, 0.0f, 0.86f), new Vector2(2.5f, -2.5f));
            }

            var progress = flow.PermanentProgress;
            var progressAnchor = selectingClass ? new Vector2(0.5f, 0.775f) : new Vector2(0.5f, 0.690f);
            var progressPanel = CreatePanel("永久进度底", uiRoot.transform, progressAnchor, progressAnchor, Vector2.zero, selectingClass ? new Vector2(720f, 46f) : new Vector2(820f, 50f), new Color(0.018f, 0.020f, 0.024f, 0.52f));
            progressPanel.raycastTarget = false;
            progressPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.74f, 0.54f, 0.30f, 0.24f);
            var progressText = CreateText("永久进度", progressPanel.transform, new Vector2(0.5f, 0.5f), progressPanel.rectTransform.sizeDelta - new Vector2(42f, 10f), 20, TextAnchor.MiddleCenter);
            progressText.text = selectingClass
                ? $"探索记录  {progress.totalRuns} 次    通关  {progress.completedRuns} 次    主角等级  {flow.PermanentHeroLevel()}"
                : $"永久进度：探索 {progress.totalRuns} 次    通关 {progress.completedRuns} 次    总经验 {progress.totalHeroExperience}    主角等级 {flow.PermanentHeroLevel()}";
            progressText.color = selectingClass ? new Color(0.80f, 0.82f, 0.72f, 0.90f) : new Color(0.86f, 0.94f, 0.92f, 0.90f);
            progressText.raycastTarget = false;

            if (titlePanelMode == TitlePanelMode.HeroClassSelect)
            {
                BuildHeroClassSelectMenu();
                return;
            }

            var start = CreateOrnateButton("开始游戏", uiRoot.transform, new Vector2(0.5f, 0.40f), new Vector2(440f, 92f), Vector2.zero, new Color(0.11f, 0.034f, 0.030f, 0.94f), new Color(0.72f, 0.52f, 0.30f, 0.76f));
            start.onClick.AddListener(() =>
            {
                titlePanelMode = TitlePanelMode.HeroClassSelect;
                titlePreviewHeroClass = GameContentFactory.DefaultHeroClass;
                titleShowFullCardPool = false;
                BuildUi();
            });

            var quit = CreateOrnateButton("退出", uiRoot.transform, new Vector2(0.5f, 0.29f), new Vector2(380f, 78f), Vector2.zero, new Color(0.030f, 0.034f, 0.040f, 0.82f), new Color(0.42f, 0.40f, 0.34f, 0.50f));
            quit.onClick.AddListener(Application.Quit);
        }

        private void BuildHeroClassSelectMenu()
        {
            var classes = AvailableHeroClasses();
            if (classes.Count == 0)
            {
                return;
            }

            if (!classes.Contains(titlePreviewHeroClass))
            {
                titlePreviewHeroClass = classes[0];
            }

            var stage = CreatePanel("职业展示舞台", uiRoot.transform, new Vector2(0.37f, 0.405f), new Vector2(0.37f, 0.405f), Vector2.zero, new Vector2(1040f, 620f), new Color(0.0f, 0.0f, 0.0f, 0.06f));
            stage.raycastTarget = false;
            AddHorizontalOrnament(stage.transform, new Vector2(0f, 304f), 860f, new Color(0.58f, 0.18f, 0.16f, 0.40f));
            AddHorizontalOrnament(stage.transform, new Vector2(0f, -304f), 860f, new Color(0.58f, 0.18f, 0.16f, 0.34f));

            var spacing = classes.Count >= 4 ? 238f : 300f;
            var startX = -(classes.Count - 1) * spacing * 0.5f;
            var placements = classes
                .Select((heroClass, index) => (HeroClass: heroClass, Offset: new Vector2(startX + index * spacing, heroClass == titlePreviewHeroClass ? 14f : -22f)))
                .ToList();

            foreach (var placement in placements.Where(placement => placement.HeroClass != titlePreviewHeroClass))
            {
                CreateHeroClassChoice(placement.HeroClass, stage.transform, placement.Offset, false);
            }

            foreach (var placement in placements.Where(placement => placement.HeroClass == titlePreviewHeroClass))
            {
                CreateHeroClassChoice(placement.HeroClass, stage.transform, placement.Offset, true);
            }

            BuildHeroClassDetailPanel(titlePreviewHeroClass);

            var back = CreateOrnateButton("返回", uiRoot.transform, new Vector2(0.50f, 0.065f), new Vector2(260f, 66f), Vector2.zero, new Color(0.10f, 0.034f, 0.030f, 0.92f), new Color(0.64f, 0.48f, 0.28f, 0.72f));
            back.onClick.AddListener(() =>
            {
                titlePanelMode = TitlePanelMode.Start;
                titleShowFullCardPool = false;
                BuildUi();
            });
        }

        private void CreateHeroClassChoice(HeroClassType heroClass, Transform parent, Vector2 offset, bool selected)
        {
            var definition = GameContentFactory.GetHeroClassDefinition(heroClass);
            var accent = HeroClassAccent(heroClass);
            var size = selected ? new Vector2(308f, 468f) : new Vector2(232f, 372f);
            var button = CreateButton(string.Empty, parent, new Vector2(0.5f, 0.5f), size, offset);
            var image = button.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);

            var blankLabel = button.GetComponentInChildren<Text>();
            if (blankLabel != null)
            {
                blankLabel.text = string.Empty;
            }

            var frameSprite = LoadClassSelectUiSprite(selected ? "class_select_frame_selected" : "class_select_frame_idle");
            var frameSize = size + (selected ? new Vector2(72f, 88f) : new Vector2(46f, 58f));
            if (AddSpriteFrame(button.transform, frameSprite, selected ? new Vector2(0f, 10f) : new Vector2(0f, 6f), frameSize) == null)
            {
                AddClassCardFrame(button.transform, size, accent, selected);
            }

            var cutoutSprite = LoadHeroClassCutoutSprite(heroClass) ?? LoadHeroClassSprite(heroClass);
            var classArt = AddSpriteIcon(button.transform, cutoutSprite, HeroClassCutoutPosition(heroClass, selected), HeroClassCutoutSize(heroClass, selected));
            if (classArt != null)
            {
                classArt.color = selected ? Color.white : new Color(0.78f, 0.80f, 0.78f, 0.78f);
            }

            var name = CreateText("职业名称", button.transform, new Vector2(0.5f, 0f), new Vector2(size.x - 58f, selected ? 42f : 34f), selected ? 29 : 23, TextAnchor.MiddleCenter);
            name.rectTransform.anchoredPosition = new Vector2(0f, selected ? 82f : 66f);
            name.text = definition.displayName;
            name.color = selected ? new Color(0.95f, 0.88f, 0.66f, 0.98f) : new Color(0.84f, 0.86f, 0.78f, 0.92f);
            name.raycastTarget = false;
            AddTextShadow(name, new Color(0f, 0f, 0f, 0.95f), new Vector2(1.6f, -1.6f));

            var style = CreateText("职业流派", button.transform, new Vector2(0.5f, 0f), new Vector2(size.x - 64f, selected ? 34f : 28f), selected ? 17 : 14, TextAnchor.MiddleCenter);
            style.rectTransform.anchoredPosition = new Vector2(0f, selected ? 48f : 38f);
            style.text = definition.shortStyle;
            style.color = selected ? new Color(0.78f, 0.90f, 0.84f, 0.94f) : new Color(0.66f, 0.72f, 0.68f, 0.82f);
            style.raycastTarget = false;
            AddTextShadow(style, new Color(0f, 0f, 0f, 0.88f), new Vector2(1.2f, -1.2f));

            if (selected)
            {
                var tag = CreatePanel("当前选择底", button.transform, new Vector2(0f, 0.72f), new Vector2(0f, 0.72f), new Vector2(16f, 0f), new Vector2(34f, 136f), new Color(0.42f, 0.058f, 0.050f, 0.92f));
                tag.raycastTarget = false;
                tag.gameObject.AddComponent<Outline>().effectColor = new Color(0.80f, 0.60f, 0.32f, 0.66f);
                var tagText = CreateText("当前选择字", tag.transform, new Vector2(0.5f, 0.5f), new Vector2(26f, 120f), 18, TextAnchor.MiddleCenter);
                tagText.text = "当\n前\n选\n择";
                tagText.color = new Color(0.96f, 0.82f, 0.54f, 0.98f);
                tagText.raycastTarget = false;
            }

            button.onClick.AddListener(() =>
            {
                titlePreviewHeroClass = heroClass;
                titleShowFullCardPool = false;
                BuildUi();
            });
        }

        private void BuildHeroClassDetailPanel(HeroClassType heroClass)
        {
            var definition = GameContentFactory.GetHeroClassDefinition(heroClass);
            var accent = HeroClassAccent(heroClass);
            var panel = CreatePanel("职业详情面板", uiRoot.transform, new Vector2(0.805f, 0.425f), new Vector2(0.805f, 0.425f), Vector2.zero, new Vector2(500f, 700f), new Color(0.010f, 0.014f, 0.018f, 0.82f));
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.56f, 0.34f, 0.46f);
            outline.effectDistance = new Vector2(2f, -2f);
            AddPanelCornerOrnaments(panel.transform, new Vector2(500f, 700f), accent);

            CreateHeroClassTab(panel.transform, "角色详情", new Vector2(-92f, -38f), !titleShowFullCardPool, accent, () =>
            {
                titleShowFullCardPool = false;
                BuildUi();
            });
            CreateHeroClassTab(panel.transform, "完整卡包", new Vector2(92f, -38f), titleShowFullCardPool, accent, () =>
            {
                titleShowFullCardPool = true;
                BuildUi();
            });

            if (titleShowFullCardPool)
            {
                BuildHeroClassFullCardPool(panel.transform, heroClass, accent);
            }
            else
            {
                BuildHeroClassOverview(panel.transform, heroClass, definition, accent);
            }

            var secondary = CreateOrnateButton(titleShowFullCardPool ? "返回角色详情" : "查看完整卡包", panel.transform, new Vector2(0.5f, 0f), new Vector2(230f, 50f), new Vector2(0f, 84f), new Color(0.038f, 0.042f, 0.048f, 0.92f), WithAlpha(accent, 0.62f));
            secondary.onClick.AddListener(() =>
            {
                titleShowFullCardPool = !titleShowFullCardPool;
                BuildUi();
            });

            var start = CreateOrnateButton("开始探索", panel.transform, new Vector2(0.5f, 0f), new Vector2(320f, 56f), new Vector2(0f, 28f), new Color(0.34f, 0.052f, 0.044f, 0.96f), new Color(0.86f, 0.62f, 0.34f, 0.82f));
            start.onClick.AddListener(() =>
            {
                sidePanelMode = SidePanelMode.None;
                titleShowFullCardPool = false;
                flow.StartNewRun(catalog, heroClass);
            });
        }

        private void BuildHeroClassOverview(Transform parent, HeroClassType heroClass, HeroClassDefinition definition, Color accent)
        {
            var portraitFrame = CreatePanel("职业头像框", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-162f, -138f), new Vector2(132f, 176f), new Color(0.006f, 0.018f, 0.018f, 0.58f));
            portraitFrame.raycastTarget = false;
            var portrait = AddSpriteIcon(portraitFrame.transform, LoadHeroClassCutoutSprite(heroClass) ?? LoadHeroClassSprite(heroClass), new Vector2(0f, 6f), new Vector2(118f, 168f));
            if (portrait != null)
            {
                portrait.color = Color.white;
            }

            if (AddSpriteFrame(portraitFrame.transform, LoadClassSelectUiSprite("class_select_frame_idle"), new Vector2(0f, 1f), new Vector2(154f, 206f)) == null)
            {
                AddClassCardFrame(portraitFrame.transform, new Vector2(132f, 176f), accent, true);
            }

            var name = CreateText("详情职业名", parent, new Vector2(0.5f, 1f), new Vector2(292f, 46f), 32, TextAnchor.MiddleLeft);
            name.rectTransform.anchoredPosition = new Vector2(86f, -104f);
            name.text = definition.displayName;
            name.color = new Color(0.95f, 0.88f, 0.66f, 0.98f);
            AddTextShadow(name, new Color(0f, 0f, 0f, 0.85f), new Vector2(1.5f, -1.5f));

            var style = CreateText("详情职业标签", parent, new Vector2(0.5f, 1f), new Vector2(292f, 34f), 18, TextAnchor.MiddleLeft);
            style.rectTransform.anchoredPosition = new Vector2(86f, -144f);
            style.text = definition.shortStyle;
            style.color = new Color(0.78f, 0.88f, 0.82f, 0.94f);

            var desc = CreateText("详情职业描述", parent, new Vector2(0.5f, 1f), new Vector2(292f, 84f), 18, TextAnchor.UpperLeft);
            desc.rectTransform.anchoredPosition = new Vector2(86f, -204f);
            desc.text = definition.description;
            desc.color = new Color(0.87f, 0.89f, 0.82f, 0.94f);

            var difficulty = CreateText("职业难度", parent, new Vector2(0.5f, 1f), new Vector2(424f, 32f), 18, TextAnchor.MiddleLeft);
            difficulty.rectTransform.anchoredPosition = new Vector2(0f, -272f);
            difficulty.text = $"上手难度    {HeroClassDifficultyStars(heroClass)}";
            difficulty.color = new Color(0.86f, 0.74f, 0.52f, 0.95f);

            var attrTitle = CreateText("职业属性标题", parent, new Vector2(0.5f, 1f), new Vector2(424f, 30f), 18, TextAnchor.MiddleLeft);
            attrTitle.rectTransform.anchoredPosition = new Vector2(0f, -310f);
            attrTitle.text = "职业倾向";
            attrTitle.color = new Color(0.82f, 0.86f, 0.80f, 0.95f);

            var attributes = HeroClassAttributeScores(heroClass);
            for (var i = 0; i < attributes.Length; i++)
            {
                CreateStatBar(parent, attributes[i].Label, attributes[i].Score, new Vector2(0f, -348f - i * 35f), accent);
            }

            var startTitle = CreateText("初始卡组标题", parent, new Vector2(0.5f, 1f), new Vector2(424f, 30f), 18, TextAnchor.MiddleLeft);
            startTitle.rectTransform.anchoredPosition = new Vector2(0f, -470f);
            startTitle.text = "初始卡组";
            startTitle.color = new Color(0.82f, 0.86f, 0.80f, 0.95f);

            var startingCards = GameContentFactory.StartingDeckCardIds(heroClass)
                .Select(GameContentFactory.BaseCardId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => catalog.FindCard(id))
                .Where(card => card != null)
                .Take(5)
                .ToList();
            var startX = -(startingCards.Count - 1) * 43f;
            for (var i = 0; i < startingCards.Count; i++)
            {
                CreateCardThumbnail(parent, startingCards[i], new Vector2(startX + i * 86f, -528f), accent);
            }
        }

        private void BuildHeroClassFullCardPool(Transform parent, HeroClassType heroClass, Color accent)
        {
            var poolIds = GameContentFactory.HeroClassCardPoolBaseIds(heroClass);
            var startingIds = new HashSet<string>(
                GameContentFactory.StartingDeckCardIds(heroClass).Select(GameContentFactory.BaseCardId),
                StringComparer.OrdinalIgnoreCase);
            var cards = poolIds
                .Select(id => new { Id = id, Card = catalog.FindCard(id) })
                .OrderBy(entry => entry.Card == null ? 99 : (int)entry.Card.type)
                .ThenBy(entry => entry.Card == null ? 99 : entry.Card.cost)
                .ThenBy(entry => entry.Card == null ? entry.Id : entry.Card.displayName)
                .ToList();

            var title = CreateText("完整卡包标题", parent, new Vector2(0.5f, 1f), new Vector2(424f, 42f), 27, TextAnchor.MiddleCenter);
            title.rectTransform.anchoredPosition = new Vector2(0f, -104f);
            title.text = $"{GameFlowController.HeroClassName(heroClass)} · 完整卡包";
            title.color = new Color(0.94f, 0.88f, 0.68f, 0.98f);

            var typeSummary = cards
                .Where(entry => entry.Card != null)
                .GroupBy(entry => CardTypeName(entry.Card.type))
                .Select(group => $"{group.Key} {group.Count()}")
                .ToList();
            var summary = CreateText("完整卡包摘要", parent, new Vector2(0.5f, 1f), new Vector2(424f, 58f), 17, TextAnchor.MiddleCenter);
            summary.rectTransform.anchoredPosition = new Vector2(0f, -154f);
            summary.text = $"卡包 {poolIds.Count} 种    初始 {GameContentFactory.StartingDeckCardIds(heroClass).Count} 张\n{string.Join("  ", typeSummary)}";
            summary.color = new Color(0.76f, 0.82f, 0.76f, 0.92f);

            var rowsPerColumn = Mathf.CeilToInt(cards.Count / 2f);
            for (var i = 0; i < cards.Count; i++)
            {
                var entry = cards[i];
                var column = i / rowsPerColumn;
                var row = i % rowsPerColumn;
                var x = column == 0 ? -108f : 108f;
                var y = -210f - row * 40f;
                CreateCardPoolEntry(parent, entry.Card, entry.Id, startingIds.Contains(entry.Id), new Vector2(x, y), accent);
            }
        }

        private void CreateHeroClassTab(Transform parent, string label, Vector2 position, bool active, Color accent, Action onClick)
        {
            var tab = CreateButton(label, parent, new Vector2(0.5f, 1f), new Vector2(162f, 38f), position);
            var image = tab.GetComponent<Image>();
            image.color = active ? WithAlpha(accent, 0.36f) : new Color(0.024f, 0.026f, 0.032f, 0.78f);
            var outline = tab.gameObject.AddComponent<Outline>();
            outline.effectColor = active ? WithAlpha(accent, 0.66f) : new Color(0.32f, 0.30f, 0.24f, 0.34f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var text = tab.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 18;
                text.resizeTextMaxSize = 18;
                text.color = active ? new Color(0.95f, 0.86f, 0.64f, 0.98f) : new Color(0.70f, 0.74f, 0.70f, 0.88f);
            }

            tab.onClick.AddListener(() => onClick?.Invoke());
        }

        private void CreateCardThumbnail(Transform parent, CardDefinition card, Vector2 position, Color accent)
        {
            var cardPanel = CreatePanel("初始卡缩略", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(78f, 104f), new Color(0.028f, 0.030f, 0.036f, 0.86f));
            cardPanel.raycastTarget = false;
            cardPanel.gameObject.AddComponent<Outline>().effectColor = WithAlpha(accent, 0.35f);
            AddSpriteIcon(cardPanel.transform, card.art, new Vector2(0f, 20f), new Vector2(64f, 50f));

            var cost = CreatePanel("卡牌费用底", cardPanel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(24f, 24f), WithAlpha(accent, 0.72f));
            cost.raycastTarget = false;
            var costText = CreateText("卡牌费用", cost.transform, new Vector2(0.5f, 0.5f), new Vector2(22f, 22f), 15, TextAnchor.MiddleCenter);
            costText.text = card.cost.ToString();
            costText.color = Color.white;
            costText.raycastTarget = false;

            var name = CreateText("初始卡名", cardPanel.transform, new Vector2(0.5f, 0.18f), new Vector2(66f, 34f), 13, TextAnchor.MiddleCenter);
            name.text = card.displayName;
            name.color = new Color(0.90f, 0.92f, 0.86f, 0.96f);
            name.raycastTarget = false;
        }

        private void CreateCardPoolEntry(Transform parent, CardDefinition card, string fallbackId, bool inStartingDeck, Vector2 position, Color accent)
        {
            var row = CreatePanel("完整卡包行", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(204f, 34f), inStartingDeck ? WithAlpha(accent, 0.20f) : new Color(0.024f, 0.028f, 0.034f, 0.76f));
            row.raycastTarget = false;
            var mark = CreatePanel("卡包行标记", row.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(5f, 24f), inStartingDeck ? new Color(0.86f, 0.62f, 0.34f, 0.88f) : WithAlpha(accent, 0.42f));
            mark.raycastTarget = false;

            var text = CreateText("完整卡包文字", row.transform, new Vector2(0.53f, 0.5f), new Vector2(180f, 26f), 13, TextAnchor.MiddleLeft);
            text.text = card == null
                ? fallbackId
                : $"{card.displayName}  {CardTypeName(card.type)}  费{card.cost}";
            text.color = inStartingDeck ? new Color(0.94f, 0.88f, 0.70f, 0.96f) : new Color(0.86f, 0.90f, 0.86f, 0.92f);
            text.raycastTarget = false;
        }

        private static void CreateStatBar(Transform parent, string label, int score, Vector2 position, Color accent)
        {
            var name = CreateText("属性名", parent, new Vector2(0.5f, 1f), new Vector2(92f, 28f), 16, TextAnchor.MiddleLeft);
            name.rectTransform.anchoredPosition = position + new Vector2(-166f, 0f);
            name.text = label;
            name.color = new Color(0.82f, 0.86f, 0.80f, 0.94f);
            name.raycastTarget = false;

            var barWidth = 218f;
            var bar = CreatePanel("属性条底", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position + new Vector2(18f, 0f), new Vector2(barWidth, 14f), new Color(0.040f, 0.044f, 0.052f, 0.92f));
            bar.raycastTarget = false;
            var fillWidth = Mathf.Clamp01(score / 5f) * barWidth;
            var fill = CreatePanel("属性条填充", bar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(fillWidth * 0.5f, 0f), new Vector2(fillWidth, 10f), WithAlpha(accent, 0.72f));
            fill.raycastTarget = false;

            var value = CreateText("属性值", parent, new Vector2(0.5f, 1f), new Vector2(42f, 28f), 15, TextAnchor.MiddleRight);
            value.rectTransform.anchoredPosition = position + new Vector2(168f, 0f);
            value.text = $"{score}/5";
            value.color = new Color(0.86f, 0.76f, 0.56f, 0.92f);
            value.raycastTarget = false;
        }

        private static Button CreateOrnateButton(string label, Transform parent, Vector2 anchor, Vector2 size, Vector2 offset, Color fill, Color border)
        {
            var button = CreateButton(label, parent, anchor, size, offset);
            var image = button.GetComponent<Image>();
            var spriteName = fill.r >= 0.20f ? "class_select_button_main" : "class_select_button_secondary";
            var sprite = LoadClassSelectUiSprite(spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                image.color = fill;
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = border;
                outline.effectDistance = new Vector2(2f, -2f);
            }

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                var maxFontSize = size.y >= 78f && label.Length <= 4 ? 24 : 20;
                text.fontSize = maxFontSize;
                text.resizeTextMaxSize = text.fontSize;
                text.color = new Color(0.94f, 0.88f, 0.72f, 0.98f);
                AddTextShadow(text, new Color(0f, 0f, 0f, 0.86f), new Vector2(1.2f, -1.2f));
            }

            if (sprite == null)
            {
                AddHorizontalOrnament(button.transform, new Vector2(0f, size.y * 0.5f - 6f), size.x - 34f, border);
                AddHorizontalOrnament(button.transform, new Vector2(0f, -size.y * 0.5f + 6f), size.x - 34f, border);
                AddDiamond(button.transform, new Vector2(-size.x * 0.5f + 18f, 0f), new Vector2(12f, 12f), border);
                AddDiamond(button.transform, new Vector2(size.x * 0.5f - 18f, 0f), new Vector2(12f, 12f), border);
            }

            return button;
        }

        private static void AddClassCardFrame(Transform parent, Vector2 size, Color accent, bool selected)
        {
            var border = selected ? new Color(0.86f, 0.62f, 0.34f, 0.88f) : WithAlpha(accent, 0.48f);
            var thin = selected ? 3.5f : 2f;
            AddFrameLine(parent, new Vector2(0f, size.y * 0.5f - 8f), new Vector2(size.x - 22f, thin), border);
            AddFrameLine(parent, new Vector2(0f, -size.y * 0.5f + 8f), new Vector2(size.x - 22f, thin), border);
            AddFrameLine(parent, new Vector2(-size.x * 0.5f + 8f, 0f), new Vector2(thin, size.y - 28f), border);
            AddFrameLine(parent, new Vector2(size.x * 0.5f - 8f, 0f), new Vector2(thin, size.y - 28f), border);

            var cornerSize = selected ? new Vector2(18f, 18f) : new Vector2(14f, 14f);
            AddDiamond(parent, new Vector2(-size.x * 0.5f + 18f, size.y * 0.5f - 18f), cornerSize, border);
            AddDiamond(parent, new Vector2(size.x * 0.5f - 18f, size.y * 0.5f - 18f), cornerSize, border);
            AddDiamond(parent, new Vector2(-size.x * 0.5f + 18f, -size.y * 0.5f + 18f), cornerSize, border);
            AddDiamond(parent, new Vector2(size.x * 0.5f - 18f, -size.y * 0.5f + 18f), cornerSize, border);
        }

        private static void AddPanelCornerOrnaments(Transform parent, Vector2 size, Color accent)
        {
            var color = new Color(0.76f, 0.56f, 0.32f, 0.52f);
            AddFrameLine(parent, new Vector2(0f, size.y * 0.5f - 12f), new Vector2(size.x - 46f, 2f), color);
            AddFrameLine(parent, new Vector2(0f, -size.y * 0.5f + 12f), new Vector2(size.x - 46f, 2f), color);
            AddDiamond(parent, new Vector2(0f, size.y * 0.5f - 12f), new Vector2(14f, 14f), WithAlpha(accent, 0.62f));
            AddDiamond(parent, new Vector2(0f, -size.y * 0.5f + 12f), new Vector2(14f, 14f), WithAlpha(accent, 0.50f));
        }

        private static void AddHorizontalOrnament(Transform parent, Vector2 position, float width, Color color)
        {
            AddFrameLine(parent, position, new Vector2(width, 2f), WithAlpha(color, color.a));
            AddDiamond(parent, position, new Vector2(12f, 12f), WithAlpha(color, Mathf.Clamp01(color.a + 0.12f)));
        }

        private static void AddFrameLine(Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var line = CreatePanel("装饰线", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, color);
            line.raycastTarget = false;
        }

        private static void AddDiamond(Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var diamond = CreatePanel("菱形纹", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, color);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            diamond.raycastTarget = false;
        }

        private static Color HeroClassAccent(HeroClassType heroClass)
        {
            return heroClass switch
            {
                HeroClassType.BorderCommander => new Color(0.78f, 0.20f, 0.16f, 1f),
                HeroClassType.SpiritSummoner => new Color(0.26f, 0.72f, 0.62f, 1f),
                HeroClassType.ThunderMage => new Color(0.30f, 0.52f, 0.95f, 1f),
                HeroClassType.TalismanSealer => new Color(0.62f, 0.34f, 0.90f, 1f),
                _ => new Color(0.52f, 0.72f, 0.68f, 1f)
            };
        }

        private static Vector2 HeroClassCutoutPosition(HeroClassType heroClass, bool selected)
        {
            if (!selected)
            {
                return heroClass switch
                {
                    HeroClassType.BorderCommander => new Vector2(0f, 68f),
                    HeroClassType.SpiritSummoner => new Vector2(0f, 54f),
                    HeroClassType.ThunderMage => new Vector2(0f, 54f),
                    HeroClassType.TalismanSealer => new Vector2(0f, 58f),
                    _ => new Vector2(0f, 58f)
                };
            }

            return heroClass switch
            {
                HeroClassType.BorderCommander => new Vector2(6f, 104f),
                HeroClassType.SpiritSummoner => new Vector2(0f, 96f),
                HeroClassType.ThunderMage => new Vector2(2f, 94f),
                HeroClassType.TalismanSealer => new Vector2(-2f, 98f),
                _ => new Vector2(0f, 96f)
            };
        }

        private static Vector2 HeroClassCutoutSize(HeroClassType heroClass, bool selected)
        {
            if (!selected)
            {
                return heroClass switch
                {
                    HeroClassType.BorderCommander => new Vector2(286f, 430f),
                    HeroClassType.SpiritSummoner => new Vector2(272f, 408f),
                    HeroClassType.ThunderMage => new Vector2(278f, 416f),
                    HeroClassType.TalismanSealer => new Vector2(268f, 402f),
                    _ => new Vector2(272f, 408f)
                };
            }

            return heroClass switch
            {
                HeroClassType.BorderCommander => new Vector2(438f, 658f),
                HeroClassType.SpiritSummoner => new Vector2(414f, 622f),
                HeroClassType.ThunderMage => new Vector2(426f, 640f),
                HeroClassType.TalismanSealer => new Vector2(404f, 606f),
                _ => new Vector2(414f, 622f)
            };
        }

        private static string HeroClassDifficultyStars(HeroClassType heroClass)
        {
            var score = heroClass switch
            {
                HeroClassType.BorderCommander => 2,
                HeroClassType.SpiritSummoner => 3,
                HeroClassType.ThunderMage => 4,
                HeroClassType.TalismanSealer => 4,
                _ => 3
            };

            return new string('★', score) + new string('☆', 5 - score);
        }

        private static (string Label, int Score)[] HeroClassAttributeScores(HeroClassType heroClass)
        {
            return heroClass switch
            {
                HeroClassType.BorderCommander => new[] { ("战线", 5), ("法术", 2), ("资源", 3), ("控制", 3) },
                HeroClassType.SpiritSummoner => new[] { ("战线", 4), ("法术", 2), ("资源", 4), ("控制", 2) },
                HeroClassType.ThunderMage => new[] { ("战线", 2), ("法术", 5), ("资源", 3), ("控制", 4) },
                HeroClassType.TalismanSealer => new[] { ("战线", 2), ("法术", 3), ("资源", 3), ("控制", 5) },
                _ => new[] { ("战线", 3), ("法术", 3), ("资源", 3), ("控制", 3) }
            };
        }

        private static IReadOnlyList<HeroClassType> AvailableHeroClasses()
        {
            return GameContentFactory.AvailableHeroClasses();
        }

        private bool CreateTitleLogo(bool selectingClass)
        {
            var sprite = LoadClassSelectUiSprite("class_select_title");
            if (sprite == null)
            {
                return false;
            }

            var titleSize = selectingClass ? new Vector2(600f, 200f) : new Vector2(570f, 190f);
            var title = CreatePanel("标题图", uiRoot.transform, selectingClass ? new Vector2(0.5f, 0.905f) : new Vector2(0.5f, 0.84f), selectingClass ? new Vector2(0.5f, 0.905f) : new Vector2(0.5f, 0.84f), Vector2.zero, titleSize, Color.white);
            title.sprite = sprite;
            title.preserveAspect = true;
            title.raycastTarget = false;
            return true;
        }

        private static Color HeroClassPanelColor(HeroClassType heroClass, bool selected)
        {
            var color = GameContentFactory.GetHeroClassDefinition(heroClass).panelColor;
            return selected ? WithAlpha(color * 1.22f, 0.96f) : color;
        }

        private void BuildRunEnd(string titleText, string body)
        {
            BuildHeader();
            var title = CreateText("结算标题", uiRoot.transform, new Vector2(0.5f, 0.64f), new Vector2(760f, 96f), 48, TextAnchor.MiddleCenter);
            title.text = titleText;

            var summary = CreateText("结算内容", uiRoot.transform, new Vector2(0.5f, 0.52f), new Vector2(1080f, 180f), 24, TextAnchor.MiddleCenter);
            var run = flow.CurrentRun;
            var permanent = flow.PermanentProgress;
            var permanentNames = permanent.permanentArtifactIds.Count == 0
                ? "暂无"
                : string.Join("、", permanent.permanentArtifactIds
                    .Select(id => catalog.FindArtifact(id)?.displayName ?? id)
                    .Take(4));
            summary.text =
                $"{body}\n" +
                $"本局：{GameFlowController.HeroClassName(run.heroClass)}    金币 {run.gold}    经验 +{run.heroExperience}    神器 {run.artifactIds.Count}    卡组 {run.deckCardIds.Count}\n" +
                $"永久：探索 {permanent.totalRuns} 次    通关 {permanent.completedRuns} 次    总经验 {permanent.totalHeroExperience}    主角等级 {flow.PermanentHeroLevel()}\n" +
                $"永久神器：{permanentNames}";

            var restart = CreateButton("重新开始探索", uiRoot.transform, new Vector2(0.5f, 0.36f), new Vector2(320f, 76f));
            restart.onClick.AddListener(() => flow.StartNewRun(catalog));

            var menu = CreateButton("返回标题", uiRoot.transform, new Vector2(0.5f, 0.26f), new Vector2(280f, 62f));
            menu.onClick.AddListener(() => flow.ReturnToTitle());
        }

        private void BuildMapPanel()
        {
            BuildHeader();
            BuildSideMenu();
            BuildSideDetailPanel();

            var run = flow.CurrentRun;
            var title = CreateText("迷宫标题", uiRoot.transform, new Vector2(0.5f, 0.825f), new Vector2(920f, 68f), 42, TextAnchor.MiddleCenter);
            title.text = $"迷宫 {run.floor} · {FloorSceneName(run.floor)}";
            title.color = new Color(0.82f, 1f, 0.96f, 0.98f);

            var currentChoices = flow.CurrentChoices().Select(node => node.Key).ToHashSet();
            var selectedNodes = flow.CurrentRun.selectedNodeKeys.ToHashSet();
            var floorRows = flow.MapRows
                .Where(row => row.Count > 0 && row[0].Floor == flow.CurrentRun.floor)
                .OrderBy(row => row[0].Row)
                .ToList();

            var mapPanel = CreatePanel("层路线图", uiRoot.transform, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(1260f, 640f), new Color(0.02f, 0.03f, 0.035f, 0.18f));
            mapPanel.raycastTarget = false;
            var mapRoot = mapPanel.rectTransform;
            var previewAnchor = sidePanelMode == SidePanelMode.None ? new Vector2(0.84f, 0.70f) : new Vector2(0.18f, 0.70f);
            var previewPanel = CreatePanel("房间情报底", uiRoot.transform, previewAnchor, previewAnchor, Vector2.zero, new Vector2(420f, 156f), new Color(0.025f, 0.030f, 0.035f, 0.56f));
            previewPanel.raycastTarget = false;
            previewPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.36f, 0.82f, 0.78f, 0.42f);
            var previewText = CreateText("房间预览", previewPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(376f, 132f), 16, TextAnchor.MiddleLeft);
            previewText.text = $"{flow.CurrentFloorAffixName()}：{flow.CurrentFloorAffixDescription()}\n把鼠标移到房间上，可以预览收益、风险和当前建议。";
            previewText.color = new Color(0.88f, 0.95f, 0.93f, 0.94f);
            previewText.raycastTarget = false;
            var positions = new Dictionary<string, Vector2>();
            foreach (var row in floorRows)
            {
                foreach (var node in row)
                {
                    positions[node.Key] = NodeMapPosition(row, node);
                }
            }

            foreach (var row in floorRows)
            {
                foreach (var node in row)
                {
                    if (!positions.TryGetValue(node.Key, out var from))
                    {
                        continue;
                    }

                    var nextRow = floorRows.FirstOrDefault(candidate => candidate.Count > 0 && candidate[0].Row == node.Row + 1);
                    if (nextRow == null)
                    {
                        continue;
                    }

                    foreach (var nextIndex in node.NextNodeIndices)
                    {
                        var nextNode = nextRow.FirstOrDefault(candidate => candidate.NodeIndex == nextIndex);
                        if (nextNode == null || !positions.TryGetValue(nextNode.Key, out var to))
                        {
                            continue;
                        }

                        var selectedLine = selectedNodes.Contains(node.Key) && selectedNodes.Contains(nextNode.Key);
                        var activeLine = selectedNodes.Contains(node.Key) || currentChoices.Contains(node.Key);
                        var color = selectedLine
                            ? new Color(0.50f, 1f, 0.88f, 0.92f)
                            : activeLine
                                ? new Color(0.42f, 0.80f, 0.82f, 0.64f)
                                : new Color(0.50f, 0.56f, 0.58f, 0.30f);
                        CreateMapLine(mapRoot, from, to, color, selectedLine ? 5.5f : 3f);
                    }
                }
            }

            foreach (var row in floorRows)
            {
                foreach (var node in row)
                {
                    var isAvailable = currentChoices.Contains(node.Key);
                    var isSelected = selectedNodes.Contains(node.Key);
                    var position = positions[node.Key];
                    var button = CreateMapRoomButton(node, mapRoot, position, isAvailable, isSelected);
                    button.interactable = isAvailable;
                    AddNodePreview(button, node, previewText, isAvailable || isSelected);
                    if (!isAvailable)
                    {
                        continue;
                    }

                    var selectedNode = node;
                    button.onClick.AddListener(() =>
                    {
                        flow.SelectNode(selectedNode);
                        if (!IsBattleNode(selectedNode.NodeType))
                        {
                            BuildUi();
                        }
                    });
                }
            }

            var progress = CreateText("路线说明", uiRoot.transform, new Vector2(0.5f, 0.095f), new Vector2(1100f, 56f), 20, TextAnchor.MiddleCenter);
            progress.text = "从下往上选择房间。亮起的房间可以进入，金色房间是已走路径；连线决定下一步可选路线。";
            progress.color = new Color(0.90f, 0.92f, 0.88f, 0.92f);

            var back = CreateButton("返回标题", uiRoot.transform, new Vector2(0.93f, 0.10f), new Vector2(112f, 62f));
            back.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.10f, 0.86f);
            back.onClick.AddListener(() => flow.ReturnToTitle());
        }

        private static Vector2 NodeMapPosition(IReadOnlyList<MapNodeRuntime> row, MapNodeRuntime node)
        {
            var center = (row.Count - 1) * 0.5f;
            var x = (node.NodeIndex - center) * 230f;
            var y = -270f + (node.Row - 1) * 62f;
            return new Vector2(x, y);
        }

        private Button CreateMapRoomButton(MapNodeRuntime node, Transform parent, Vector2 offset, bool available, bool selected)
        {
            var button = CreateButton(string.Empty, parent, new Vector2(0.5f, 0.5f), new Vector2(86f, 86f), offset);
            var image = button.GetComponent<Image>();
            image.sprite = CircleSprite();
            image.color = selected
                ? new Color(0.20f, 0.72f, 0.68f, 0.95f)
                : available
                    ? WithAlpha(NodeColor(node.NodeType), 0.92f)
                    : new Color(0.10f, 0.12f, 0.13f, 0.70f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            var halo = AddNodeHalo(button.transform, selected, available);
            halo.transform.SetAsFirstSibling();
            AddSpriteIcon(button.transform, LoadNodeSprite(node.NodeType), new Vector2(0f, 6f), new Vector2(54f, 54f));

            var labelPanel = CreatePanel("节点标签底", button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(96f, 24f), selected ? new Color(0.20f, 0.38f, 0.34f, 0.84f) : WithAlpha(NodeColor(node.NodeType), available ? 0.76f : 0.48f));
            labelPanel.raycastTarget = false;
            labelPanel.gameObject.AddComponent<Outline>().effectColor = selected ? new Color(0.78f, 0.96f, 0.84f, 0.52f) : new Color(0f, 0f, 0f, 0.36f);
            var nodeLabel = CreateText("节点标签", labelPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(90f, 20f), 16, TextAnchor.MiddleCenter);
            nodeLabel.text = NodeShortName(node.NodeType);
            nodeLabel.color = selected ? new Color(0.82f, 1f, 0.96f) : available ? Color.white : new Color(0.70f, 0.73f, 0.72f, 0.86f);
            nodeLabel.raycastTarget = false;

            if (selected || available)
            {
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = selected ? new Color(0.50f, 1f, 0.88f, 0.98f) : new Color(0.62f, 0.86f, 1f, 0.78f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            return button;
        }

        private void AddNodePreview(Button button, MapNodeRuntime node, Text previewText, bool reachable)
        {
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => previewText.text = flow.BuildNodePreview(node, reachable));
            AddTrigger(trigger, EventTriggerType.PointerClick, () => previewText.text = flow.BuildNodePreview(node, reachable));
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
        }

        private static Image CreateMapLine(Transform parent, Vector2 from, Vector2 to, Color color, float thickness)
        {
            var go = new GameObject("路线", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var delta = to - from;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static string NodeShortName(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.NormalMonster => "普通",
                MapNodeType.EliteMonster => "精英",
                MapNodeType.Shop => "商店",
                MapNodeType.Rest => "休息",
                MapNodeType.Opportunity => "机遇",
                MapNodeType.Mystery => "神秘",
                MapNodeType.Artifact => "神器",
                MapNodeType.SmallBoss => "小首领",
                MapNodeType.FinalBoss => "最终首领",
                _ => "房间"
            };
        }

        private static string NodeIconName(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.NormalMonster => "node_normal_monster",
                MapNodeType.EliteMonster => "node_elite_monster",
                MapNodeType.Shop => "node_shop",
                MapNodeType.Rest => "node_rest",
                MapNodeType.Opportunity => "node_opportunity",
                MapNodeType.Mystery => "node_mystery",
                MapNodeType.Artifact => "node_artifact",
                MapNodeType.SmallBoss => "node_small_boss",
                MapNodeType.FinalBoss => "node_final_boss",
                _ => "icon_route_path"
            };
        }

        private static Color NodeColor(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.NormalMonster => new Color(0.12f, 0.24f, 0.24f, 0.96f),
                MapNodeType.EliteMonster => new Color(0.34f, 0.09f, 0.12f, 0.96f),
                MapNodeType.Shop => new Color(0.11f, 0.20f, 0.30f, 0.96f),
                MapNodeType.Rest => new Color(0.05f, 0.28f, 0.22f, 0.96f),
                MapNodeType.Opportunity => new Color(0.22f, 0.20f, 0.36f, 0.96f),
                MapNodeType.Mystery => new Color(0.16f, 0.13f, 0.22f, 0.96f),
                MapNodeType.Artifact => new Color(0.18f, 0.11f, 0.30f, 0.96f),
                MapNodeType.SmallBoss or MapNodeType.FinalBoss => new Color(0.40f, 0.04f, 0.08f, 0.96f),
                _ => new Color(0.15f, 0.2f, 0.28f, 0.95f)
            };
        }

        private static string FloorSceneName(int floor)
        {
            return floor switch
            {
                1 => "迷雾谷",
                2 => "雷劫台",
                3 => "混沌宫",
                _ => "未知境"
            };
        }

        private static string ArtifactRarityName(ArtifactRarity rarity)
        {
            return rarity switch
            {
                ArtifactRarity.Common => "阵法",
                ArtifactRarity.Rare => "奇术",
                ArtifactRarity.Epic => "法宝",
                ArtifactRarity.Legendary => "神物",
                _ => "强化"
            };
        }

        private static Color ArtifactRarityColor(ArtifactRarity rarity)
        {
            return rarity switch
            {
                ArtifactRarity.Common => new Color(0.48f, 0.86f, 0.72f, 0.92f),
                ArtifactRarity.Rare => new Color(0.70f, 0.56f, 1f, 0.92f),
                ArtifactRarity.Epic => new Color(0.86f, 0.62f, 1f, 0.94f),
                ArtifactRarity.Legendary => new Color(1f, 0.35f, 0.28f, 0.96f),
                _ => new Color(0.76f, 0.86f, 0.84f, 0.92f)
            };
        }

        private static Color ArtifactPanelColor(ArtifactRarity rarity)
        {
            var color = rarity switch
            {
                ArtifactRarity.Common => new Color(0.03f, 0.12f, 0.10f, 0.93f),
                ArtifactRarity.Rare => new Color(0.10f, 0.06f, 0.17f, 0.94f),
                ArtifactRarity.Epic => new Color(0.13f, 0.07f, 0.20f, 0.94f),
                ArtifactRarity.Legendary => new Color(0.20f, 0.055f, 0.04f, 0.94f),
                _ => new Color(0.08f, 0.08f, 0.09f, 0.94f)
            };
            return color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Image AddNodeHalo(Transform parent, bool selected, bool available)
        {
            var go = new GameObject("徽章光环", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(104f, 104f);
            rect.anchoredPosition = Vector2.zero;
            var halo = go.GetComponent<Image>();
            halo.sprite = CircleSprite();
            halo.raycastTarget = false;
            halo.color = selected
                ? new Color(0.40f, 1f, 0.88f, 0.34f)
                : available
                    ? new Color(0.72f, 0.92f, 1f, 0.20f)
                    : new Color(0.04f, 0.04f, 0.04f, 0.12f);
            return halo;
        }

        private static Sprite CircleSprite()
        {
            if (cachedCircleSprite != null)
            {
                return cachedCircleSprite;
            }

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "XTD_RuntimeCircle"
            };

            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01((1f - distance) * 12f);
                    var rim = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.80f) * 16f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(alpha * 0.82f, rim * 0.55f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return cachedCircleSprite;
        }

        private static string CardTypeName(CardType type)
        {
            if (type == CardType.Curse)
            {
                return "诅咒";
            }

            return type switch
            {
                CardType.Structure => "建筑",
                CardType.Soldier => "士兵",
                CardType.EliteSoldier => "精兵",
                CardType.Hero => "英雄",
                CardType.Spell => "法术",
                CardType.Tactic => "战术",
                CardType.Debuff => "压制",
                CardType.Economy => "调度",
                _ => "卡牌"
            };
        }

        private static Color CardPanelColor(CardType type)
        {
            if (type == CardType.Curse)
            {
                return new Color(0.14f, 0.06f, 0.18f, 0.96f);
            }

            return type switch
            {
                CardType.Structure => new Color(0.08f, 0.22f, 0.24f, 0.96f),
                CardType.Spell => new Color(0.30f, 0.06f, 0.12f, 0.96f),
                CardType.Tactic => new Color(0.08f, 0.24f, 0.18f, 0.96f),
                CardType.Debuff => new Color(0.20f, 0.10f, 0.24f, 0.96f),
                CardType.Economy => new Color(0.16f, 0.18f, 0.28f, 0.96f),
                CardType.EliteSoldier or CardType.Hero => new Color(0.22f, 0.08f, 0.12f, 0.96f),
                _ => new Color(0.13f, 0.20f, 0.23f, 0.96f)
            };
        }

        private static string CardRarityName(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Uncommon => "良品",
                CardRarity.Rare => "珍稀",
                CardRarity.Epic => "秘传",
                CardRarity.Legendary => "神授",
                _ => "凡品"
            };
        }

        private static Color CardRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Uncommon => new Color(0.50f, 0.92f, 0.72f, 0.92f),
                CardRarity.Rare => new Color(0.54f, 0.74f, 1f, 0.92f),
                CardRarity.Epic => new Color(0.82f, 0.58f, 1f, 0.94f),
                CardRarity.Legendary => new Color(1f, 0.38f, 0.32f, 0.96f),
                _ => new Color(0.76f, 0.86f, 0.84f, 0.92f)
            };
        }

        private Button CreateCardChoiceCard(
            CardDefinition card,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Vector2 offset,
            string headerTag,
            string actionText,
            string badgeText = null,
            string descOverride = null,
            bool interactable = true)
        {
            var button = CreateButton(string.Empty, parent, anchor, size, offset);
            button.interactable = interactable;
            var image = button.GetComponent<Image>();
            image.color = interactable
                ? CardPanelColor(card.type)
                : WithAlpha(CardPanelColor(card.type), 0.56f);

            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(CardRarityColor(card.rarity), interactable ? 0.72f : 0.34f);
            outline.effectDistance = new Vector2(2f, -2f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            var topBand = CreatePanel("卡牌标题带", button.transform, new Vector2(0.5f, 0.93f), new Vector2(0.5f, 0.93f), Vector2.zero, new Vector2(size.x - 32f, 28f), new Color(0.018f, 0.030f, 0.034f, 0.60f));
            topBand.raycastTarget = false;
            var topTag = CreateText("卡牌标题标签", topBand.transform, new Vector2(0.14f, 0.5f), new Vector2(94f, 24f), 16, TextAnchor.MiddleCenter);
            topTag.text = headerTag;
            topTag.color = new Color(0.72f, 0.96f, 0.92f, interactable ? 0.96f : 0.68f);
            topTag.raycastTarget = false;

            var rarityBadge = CreatePanel("卡牌稀有度", button.transform, new Vector2(0.84f, 0.93f), new Vector2(0.84f, 0.93f), Vector2.zero, new Vector2(86f, 24f), WithAlpha(CardRarityColor(card.rarity), interactable ? 0.34f : 0.18f));
            rarityBadge.raycastTarget = false;
            var rarityText = CreateText("卡牌稀有度文字", rarityBadge.transform, new Vector2(0.5f, 0.5f), new Vector2(82f, 22f), 15, TextAnchor.MiddleCenter);
            rarityText.text = string.IsNullOrWhiteSpace(badgeText) ? CardRarityName(card.rarity) : badgeText;
            rarityText.color = new Color(0.92f, 0.98f, 0.96f, interactable ? 0.96f : 0.68f);
            rarityText.raycastTarget = false;

            var artPanel = CreatePanel("卡牌卡图底", button.transform, new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(size.x - 34f, size.y * 0.34f), new Color(0.018f, 0.030f, 0.034f, 0.60f));
            artPanel.raycastTarget = false;
            artPanel.gameObject.AddComponent<RectMask2D>();
            var art = AddSpriteIcon(artPanel.transform, card.art, Vector2.zero, artPanel.rectTransform.sizeDelta);
            if (art != null)
            {
                art.preserveAspect = false;
                art.color = new Color(1f, 1f, 1f, interactable ? 1f : 0.58f);
                FitSpriteToCover(art.rectTransform, artPanel.rectTransform.sizeDelta.x, artPanel.rectTransform.sizeDelta.y, card.art);
            }

            var name = CreateText("卡牌名称", button.transform, new Vector2(0.5f, 0.43f), new Vector2(size.x - 38f, 34f), 24, TextAnchor.MiddleCenter);
            name.text = card.displayName;
            name.color = new Color(0.90f, 1f, 0.96f, interactable ? 0.98f : 0.72f);
            name.raycastTarget = false;
            AddTextShadow(name, new Color(0f, 0.02f, 0.025f, 0.88f), new Vector2(1.2f, -1.2f));

            var meta = CreateText("卡牌属性", button.transform, new Vector2(0.5f, 0.33f), new Vector2(size.x - 38f, 30f), 16, TextAnchor.MiddleCenter);
            meta.text = $"{CardTypeName(card.type)}  费用 {card.cost}  Lv.{card.level}";
            meta.color = new Color(0.82f, 0.96f, 0.92f, interactable ? 0.94f : 0.68f);
            meta.raycastTarget = false;

            var desc = CreateText("卡牌描述", button.transform, new Vector2(0.5f, 0.18f), new Vector2(size.x - 42f, size.y * 0.20f), 16, TextAnchor.MiddleCenter);
            desc.text = string.IsNullOrWhiteSpace(descOverride) ? card.description : descOverride;
            desc.color = new Color(0.90f, 0.95f, 0.93f, interactable ? 0.94f : 0.68f);
            desc.raycastTarget = false;

            var actionBand = CreatePanel("卡牌操作带", button.transform, new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0.07f), Vector2.zero, new Vector2(size.x - 44f, 32f), interactable ? new Color(0.018f, 0.032f, 0.034f, 0.62f) : new Color(0.018f, 0.032f, 0.034f, 0.34f));
            actionBand.raycastTarget = false;
            var action = CreateText("卡牌操作文字", actionBand.transform, new Vector2(0.5f, 0.5f), new Vector2(size.x - 52f, 24f), 17, TextAnchor.MiddleCenter);
            action.text = actionText;
            action.color = new Color(0.62f, 0.96f, 0.90f, interactable ? 0.96f : 0.66f);
            action.raycastTarget = false;
            return button;
        }

        private Button CreateFeatureChoiceCard(
            string title,
            string headerTag,
            string description,
            string actionText,
            Sprite art,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Vector2 offset,
            Color color,
            string badgeText = null,
            bool interactable = true)
        {
            var button = CreateButton(string.Empty, parent, anchor, size, offset);
            button.interactable = interactable;
            var image = button.GetComponent<Image>();
            image.color = interactable ? color : WithAlpha(color, 0.56f);

            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = interactable ? new Color(0.42f, 0.88f, 0.82f, 0.58f) : new Color(0.48f, 0.48f, 0.48f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            var topBand = CreatePanel("功能标题带", button.transform, new Vector2(0.5f, 0.92f), new Vector2(0.5f, 0.92f), Vector2.zero, new Vector2(size.x - 34f, 28f), new Color(0.018f, 0.030f, 0.034f, 0.60f));
            topBand.raycastTarget = false;
            var topTag = CreateText("功能标题标签", topBand.transform, new Vector2(0.16f, 0.5f), new Vector2(88f, 24f), 16, TextAnchor.MiddleCenter);
            topTag.text = headerTag;
            topTag.color = new Color(0.72f, 0.96f, 0.92f, interactable ? 0.96f : 0.68f);
            topTag.raycastTarget = false;

            if (!string.IsNullOrWhiteSpace(badgeText))
            {
                var badge = CreatePanel("功能角标", button.transform, new Vector2(0.84f, 0.92f), new Vector2(0.84f, 0.92f), Vector2.zero, new Vector2(98f, 24f), new Color(0.38f, 0.86f, 0.80f, interactable ? 0.24f : 0.12f));
                badge.raycastTarget = false;
                var badgeLabel = CreateText("功能角标文字", badge.transform, new Vector2(0.5f, 0.5f), new Vector2(92f, 22f), 15, TextAnchor.MiddleCenter);
                badgeLabel.text = badgeText;
                badgeLabel.color = new Color(0.92f, 0.98f, 0.96f, interactable ? 0.96f : 0.68f);
                badgeLabel.raycastTarget = false;
            }

            var artPanel = CreatePanel("功能卡图底", button.transform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(size.x - 34f, size.y * 0.32f), new Color(0.018f, 0.030f, 0.034f, 0.60f));
            artPanel.raycastTarget = false;
            artPanel.gameObject.AddComponent<RectMask2D>();
            var artImage = AddSpriteIcon(artPanel.transform, art, Vector2.zero, artPanel.rectTransform.sizeDelta);
            if (artImage != null)
            {
                artImage.preserveAspect = false;
                artImage.color = new Color(1f, 1f, 1f, interactable ? 1f : 0.58f);
                FitSpriteToCover(artImage.rectTransform, artPanel.rectTransform.sizeDelta.x, artPanel.rectTransform.sizeDelta.y, art);
            }

            var name = CreateText("功能名称", button.transform, new Vector2(0.5f, 0.40f), new Vector2(size.x - 38f, 38f), 24, TextAnchor.MiddleCenter);
            name.text = title;
            name.color = new Color(0.90f, 1f, 0.96f, interactable ? 0.98f : 0.72f);
            name.raycastTarget = false;

            var desc = CreateText("功能描述", button.transform, new Vector2(0.5f, 0.20f), new Vector2(size.x - 42f, size.y * 0.20f), 16, TextAnchor.MiddleCenter);
            desc.text = description;
            desc.color = new Color(0.90f, 0.95f, 0.93f, interactable ? 0.94f : 0.68f);
            desc.raycastTarget = false;

            var actionBand = CreatePanel("功能操作带", button.transform, new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0.07f), Vector2.zero, new Vector2(size.x - 44f, 32f), interactable ? new Color(0.018f, 0.032f, 0.034f, 0.62f) : new Color(0.018f, 0.032f, 0.034f, 0.34f));
            actionBand.raycastTarget = false;
            var action = CreateText("功能操作文字", actionBand.transform, new Vector2(0.5f, 0.5f), new Vector2(size.x - 52f, 24f), 17, TextAnchor.MiddleCenter);
            action.text = actionText;
            action.color = new Color(0.62f, 0.96f, 0.90f, interactable ? 0.96f : 0.66f);
            action.raycastTarget = false;
            return button;
        }

        private Button CreateEventChoiceCard(
            OpportunityEventPreview preview,
            string headerTag,
            string actionText,
            Sprite art,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            Vector2 offset,
            Color fill,
            Color accent)
        {
            var button = CreateButton(string.Empty, parent, anchor, size, offset);
            var image = button.GetComponent<Image>();
            image.color = fill;

            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(2f, -2f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            AddClassCardFrame(button.transform, size - new Vector2(10f, 10f), accent, false);

            var artPanel = CreatePanel("事件插画底", button.transform, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(size.x - 44f, size.y * 0.34f), new Color(0.010f, 0.020f, 0.024f, 0.50f));
            artPanel.raycastTarget = false;
            artPanel.gameObject.AddComponent<RectMask2D>();
            var artImage = AddSpriteIcon(artPanel.transform, art, Vector2.zero, artPanel.rectTransform.sizeDelta);
            if (artImage != null)
            {
                artImage.preserveAspect = false;
                FitSpriteToCover(artImage.rectTransform, artPanel.rectTransform.sizeDelta.x, artPanel.rectTransform.sizeDelta.y, art);
            }

            var topBand = CreatePanel("事件标题带", button.transform, new Vector2(0.5f, 0.91f), new Vector2(0.5f, 0.91f), Vector2.zero, new Vector2(size.x - 58f, 30f), new Color(0.012f, 0.022f, 0.026f, 0.68f));
            topBand.raycastTarget = false;
            var topTag = CreateText("事件类型", topBand.transform, new Vector2(0.16f, 0.5f), new Vector2(88f, 24f), 16, TextAnchor.MiddleCenter);
            topTag.text = headerTag;
            topTag.color = new Color(0.76f, 0.94f, 0.90f, 0.96f);
            topTag.raycastTarget = false;

            var badge = CreatePanel("事件风险标", topBand.transform, new Vector2(0.78f, 0.5f), new Vector2(0.78f, 0.5f), Vector2.zero, new Vector2(100f, 22f), WithAlpha(accent, 0.28f));
            badge.raycastTarget = false;
            var badgeText = CreateText("事件风险字", badge.transform, new Vector2(0.5f, 0.5f), new Vector2(94f, 20f), 14, TextAnchor.MiddleCenter);
            badgeText.text = RiskBadgeText(preview.risk);
            badgeText.color = new Color(0.94f, 0.98f, 0.96f, 0.94f);
            badgeText.raycastTarget = false;

            var name = CreateText("事件名称", button.transform, new Vector2(0.5f, 0.455f), new Vector2(size.x - 58f, 42f), 27, TextAnchor.MiddleCenter);
            name.text = preview.title;
            name.color = new Color(0.95f, 0.88f, 0.66f, 0.98f);
            name.raycastTarget = false;
            AddTextShadow(name, new Color(0f, 0f, 0f, 0.88f), new Vector2(1.2f, -1.2f));

            var story = CreateText("事件叙事", button.transform, new Vector2(0.5f, 0.345f), new Vector2(size.x - 58f, 54f), 16, TextAnchor.MiddleCenter);
            story.text = preview.story;
            story.color = new Color(0.88f, 0.94f, 0.92f, 0.92f);
            story.raycastTarget = false;

            AddEventInfoStrip(button.transform, "收益", preview.reward, new Vector2(0f, -104f), new Vector2(size.x - 66f, 34f), new Color(0.24f, 0.72f, 0.56f, 0.22f));
            AddEventInfoStrip(button.transform, "风险", preview.risk, new Vector2(0f, -142f), new Vector2(size.x - 66f, 34f), new Color(0.82f, 0.36f, 0.32f, 0.18f));

            var actionBand = CreatePanel("事件操作带", button.transform, new Vector2(0.5f, 0.035f), new Vector2(0.5f, 0.035f), Vector2.zero, new Vector2(size.x - 74f, 30f), new Color(0.018f, 0.034f, 0.036f, 0.70f));
            actionBand.raycastTarget = false;
            var action = CreateText("事件操作文字", actionBand.transform, new Vector2(0.5f, 0.5f), new Vector2(size.x - 84f, 22f), 16, TextAnchor.MiddleCenter);
            action.text = actionText;
            action.color = new Color(0.62f, 0.96f, 0.90f, 0.96f);
            action.raycastTarget = false;

            return button;
        }

        private static void AddEventInfoStrip(Transform parent, string label, string value, Vector2 position, Vector2 size, Color color)
        {
            var strip = CreatePanel("事件信息条", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, color);
            strip.raycastTarget = false;
            var labelText = CreateText("事件信息标签", strip.transform, new Vector2(0.13f, 0.5f), new Vector2(48f, size.y - 8f), 14, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.color = new Color(0.78f, 0.94f, 0.88f, 0.92f);
            labelText.raycastTarget = false;

            var valueText = CreateText("事件信息内容", strip.transform, new Vector2(0.60f, 0.5f), new Vector2(size.x - 74f, size.y - 8f), 14, TextAnchor.MiddleLeft);
            valueText.text = value;
            valueText.color = new Color(0.92f, 0.96f, 0.94f, 0.94f);
            valueText.raycastTarget = false;
        }

        private static string RiskBadgeText(string risk)
        {
            if (string.IsNullOrWhiteSpace(risk) || risk.Contains("无直接风险", StringComparison.Ordinal))
            {
                return "低风险";
            }

            return risk.Contains("惩罚", StringComparison.Ordinal) || risk.Contains("诅咒", StringComparison.Ordinal) || risk.Contains("失去", StringComparison.Ordinal)
                ? "高风险"
                : "小风险";
        }

        private static string MysteryActionText(int templateIndex)
        {
            return templateIndex switch
            {
                0 => "稳妥探查",
                1 => "深入凶阵",
                _ => "献祭换宝"
            };
        }

        private static Sprite OpportunityEventArt(int templateIndex)
        {
            var fileName = templateIndex switch
            {
                0 => "event_opportunity_caravan",
                1 => "event_opportunity_talisman",
                2 => "event_opportunity_forge",
                3 => "event_opportunity_lotus_spring",
                4 => "event_opportunity_demon_market",
                _ => "event_opportunity_star_scroll"
            };

            return LoadEventUiSprite(fileName) ?? LoadNodeSprite(MapNodeType.Opportunity);
        }

        private static Sprite MysteryEventArt(int templateIndex)
        {
            var fileName = templateIndex switch
            {
                0 => "event_mystery_scout",
                1 => "event_mystery_array",
                _ => "event_mystery_sacrifice"
            };

            return LoadEventUiSprite(fileName) ?? LoadNodeSprite(MapNodeType.Mystery);
        }

        private static Sprite ArtifactCardFrameSprite(ArtifactRarity rarity)
        {
            var fileName = rarity switch
            {
                ArtifactRarity.Common => "artifact_select_card_common",
                ArtifactRarity.Legendary => "artifact_select_card_legendary",
                _ => "artifact_select_card_rare"
            };

            return LoadArtifactSelectUiSprite(fileName);
        }

        private static void FitSpriteToCover(RectTransform rect, float frameWidth, float frameHeight, Sprite sprite)
        {
            if (rect == null)
            {
                return;
            }

            if (sprite == null)
            {
                rect.sizeDelta = new Vector2(frameWidth, frameHeight);
                rect.anchoredPosition = Vector2.zero;
                return;
            }

            var spriteRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            var frameRatio = frameWidth / Mathf.Max(1f, frameHeight);
            float width;
            float height;

            if (spriteRatio >= frameRatio)
            {
                height = frameHeight;
                width = height * spriteRatio;
            }
            else
            {
                width = frameWidth;
                height = width / Mathf.Max(0.01f, spriteRatio);
            }

            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private void BuildLegacyChoicePanel()
        {
            var choices = flow.CurrentChoices();
            var startX = -((choices.Count - 1) * 220f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var node = choices[i];
                var button = CreateNodeButton(node, uiRoot.transform, new Vector2(0.5f, 0.55f), new Vector2(startX + i * 220f, 0f));
                button.onClick.AddListener(() =>
                {
                    flow.SelectNode(node);
                    if (!IsBattleNode(node.NodeType))
                    {
                        BuildUi();
                    }
                });
            }
        }

        private void BuildNodePanel(MapNodeRuntime node)
        {
            BuildHeader();

            if (node.NodeType != MapNodeType.Artifact)
            {
                var title = CreateText("节点标题", uiRoot.transform, new Vector2(0.5f, 0.77f), new Vector2(900f, 72f), 36, TextAnchor.MiddleCenter);
                title.text = $"{GameFlowController.NodeTypeName(node.NodeType)}";
                title.color = new Color(0.84f, 1f, 0.96f, 0.98f);
                AddTextShadow(title, new Color(0f, 0f, 0f, 0.88f), new Vector2(1.5f, -1.5f));
            }

            switch (node.NodeType)
            {
                case MapNodeType.Shop:
                    BuildShopPanel();
                    break;
                case MapNodeType.Rest:
                    BuildRestPanel();
                    break;
                case MapNodeType.Opportunity:
                    BuildOpportunityChoicesPanel();
                    break;
                case MapNodeType.Mystery:
                    BuildMysteryPanel();
                    break;
                case MapNodeType.Artifact:
                    BuildArtifactPanel();
                    break;
                default:
                    BuildMapPanel();
                    break;
            }
        }

        private void BuildCardRewardPanel()
        {
            BuildHeader();

            var title = CreateText("奖励标题", uiRoot.transform, new Vector2(0.5f, 0.74f), new Vector2(920f, 72f), 36, TextAnchor.MiddleCenter);
            title.text = $"卡牌奖励：还可选择 {flow.PendingCardRewardPickCount} 张";

            var info = CreateText("奖励说明", uiRoot.transform, new Vector2(0.5f, 0.68f), new Vector2(1020f, 42f), 20, TextAnchor.MiddleCenter);
            info.text = "从战利品中带走新的构筑部件。卡图、费用和等级会直接决定你这一局的节奏。";
            info.color = new Color(0.86f, 0.94f, 0.92f, 0.94f);

            var choices = flow.PendingCardRewardChoices();
            var startX = -((choices.Count - 1) * 282f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var card = choices[i];
                var button = CreateCardChoiceCard(
                    card,
                    uiRoot.transform,
                    new Vector2(0.5f, 0.46f),
                    new Vector2(252f, 316f),
                    new Vector2(startX + i * 282f, 0f),
                    "战利品",
                    "选入卡组",
                    CardRarityName(card.rarity));
                button.onClick.AddListener(() =>
                {
                    flow.ChooseCardReward(card);
                    BuildUi();
                });
            }

            var skipLabel = flow.PendingCardRewardSkipGold > 0
                ? $"放弃剩余奖励，换得 {flow.PendingCardRewardResolvedSkipGold} 金"
                : "放弃剩余奖励";
            var skip = CreateOrnateButton(skipLabel, uiRoot.transform, new Vector2(0.5f, 0.14f), new Vector2(340f, 68f), Vector2.zero, new Color(0.040f, 0.050f, 0.064f, 0.90f), new Color(0.58f, 0.48f, 0.32f, 0.58f));
            skip.onClick.AddListener(() =>
            {
                flow.SkipCardRewardForGold();
                BuildUi();
            });
        }

        private void BuildShopPanel()
        {
            var info = CreateText("商店说明", uiRoot.transform, new Vector2(0.5f, 0.705f), new Vector2(1120f, 46f), 21, TextAnchor.MiddleCenter);
            info.text = $"当前金币 {flow.CurrentRun.gold}。购买会移出当前货架；可以刷新商品、半价出售，或花金币净化移除 1 张牌。";

            var shopCards = flow.GenerateShopCards();
            var buyTitle = CreateText("进货标题", uiRoot.transform, new Vector2(0.5f, 0.650f), new Vector2(1000f, 34f), 19, TextAnchor.MiddleCenter);
            buyTitle.text = "货架陈列";
            buyTitle.color = new Color(0.62f, 0.96f, 0.90f, 0.96f);
            if (shopCards.Count == 0)
            {
                var empty = CreateText("商店售罄", uiRoot.transform, new Vector2(0.5f, 0.56f), new Vector2(720f, 70f), 24, TextAnchor.MiddleCenter);
                empty.text = "当前货架已经买空，可以刷新商品。";
                empty.color = new Color(0.86f, 0.94f, 0.92f, 0.95f);
            }

            for (var i = 0; i < shopCards.Count; i++)
            {
                var card = shopCards[i];
                var buyPrice = flow.CardBuyPrice(card);
                var canBuy = flow.CurrentRun.gold >= buyPrice;
                var button = CreateCardChoiceCard(
                    card,
                    uiRoot.transform,
                    new Vector2(0.5f, 0.545f),
                    new Vector2(176f, 236f),
                    new Vector2(-376f + i * 188f, 0f),
                    "商店",
                    canBuy ? "购入卡组" : "金币不足",
                    $"{buyPrice} 金",
                    null,
                    canBuy);
                button.onClick.AddListener(() =>
                {
                    flow.BuyCard(card);
                    BuildUi();
                });
            }

            var reroll = CreateOrnateButton($"刷新货架\n{flow.ShopRerollCost} 金币", uiRoot.transform, new Vector2(0.83f, 0.55f), new Vector2(210f, 76f), Vector2.zero, flow.CurrentRun.gold >= flow.ShopRerollCost ? new Color(0.05f, 0.20f, 0.22f, 0.94f) : new Color(0.08f, 0.08f, 0.08f, 0.70f), new Color(0.54f, 0.82f, 0.72f, 0.42f));
            reroll.onClick.AddListener(() =>
            {
                flow.RerollShopCards();
                BuildUi();
            });

            var sellTitle = CreateText("出售标题", uiRoot.transform, new Vector2(0.5f, 0.400f), new Vector2(1000f, 32f), 18, TextAnchor.MiddleCenter);
            sellTitle.text = "半价出售已有卡牌";
            var sellCards = flow.CurrentRun.deckCardIds
                .GroupBy(id => id)
                .Select(group => new { Id = group.Key, Count = group.Count(), Card = catalog.FindCard(group.Key) })
                .Where(entry => entry.Card != null)
                .OrderBy(entry => entry.Card.type)
                .ThenBy(entry => entry.Card.cost)
                .ThenBy(entry => entry.Card.displayName)
                .Take(6)
                .ToList();
            for (var i = 0; i < sellCards.Count; i++)
            {
                var entry = sellCards[i];
                var sellPrice = Mathf.Max(1, flow.CardBuyPrice(entry.Card) / 2);
                var button = CreateCardChoiceCard(
                    entry.Card,
                    uiRoot.transform,
                    new Vector2(0.5f, 0.305f),
                    new Vector2(138f, 136f),
                    new Vector2(-390f + i * 156f, 0f),
                    "出售",
                    "卖出一张",
                    $"+{sellPrice} 金",
                    $"现有 {entry.Count} 张\n{entry.Card.description}");
                button.onClick.AddListener(() =>
                {
                    flow.SellCard(entry.Id);
                    BuildUi();
                });
            }

            var removeTitle = CreateText("净化标题", uiRoot.transform, new Vector2(0.5f, 0.205f), new Vector2(880f, 30f), 16, TextAnchor.MiddleCenter);
            removeTitle.text = flow.CanRemoveCardAtShop
                ? $"净化移除一张牌：{flow.ShopRemoveCost} 金币，本次商店限 1 次"
                : "净化移除：本次不可用，可能已使用、金币不足或卡组过薄";
            removeTitle.color = new Color(0.86f, 0.94f, 0.92f, 0.94f);

            var removeCards = sellCards.Take(4).ToList();
            for (var i = 0; i < removeCards.Count; i++)
            {
                var entry = removeCards[i];
                var canRemove = flow.CanRemoveCardAtShop && flow.CurrentRun.gold >= flow.ShopRemoveCost;
                var button = CreateCardChoiceCard(
                    entry.Card,
                    uiRoot.transform,
                    new Vector2(0.5f, 0.125f),
                    new Vector2(128f, 102f),
                    new Vector2(-225f + i * 150f, 0f),
                    "净化",
                    canRemove ? "移出卡组" : "当前不可用",
                    $"{flow.ShopRemoveCost} 金",
                    entry.Card.description,
                    canRemove);
                button.onClick.AddListener(() =>
                {
                    flow.RemoveCardAtShop(entry.Id);
                    BuildUi();
                });
            }

            var leave = CreateOrnateButton("离开商店", uiRoot.transform, new Vector2(0.83f, 0.220f), new Vector2(220f, 60f), Vector2.zero, new Color(0.040f, 0.050f, 0.064f, 0.90f), new Color(0.58f, 0.48f, 0.32f, 0.58f));
            leave.onClick.AddListener(() =>
            {
                flow.LeaveShop();
                BuildUi();
            });
        }

        private void BuildRestPanel()
        {
            var info = CreateText("休息说明", uiRoot.transform, new Vector2(0.5f, 0.700f), new Vector2(1080f, 46f), 21, TextAnchor.MiddleCenter);
            info.text = "选择回血，或三张同级同名卡牌合成一张高一级卡牌。每次休息只能做一件事。";

            var groups = flow.UpgradableCardGroups().ToList();
            var visibleGroups = groups.Take(4).ToList();
            var spacing = visibleGroups.Count <= 1 ? 360f : 208f;
            var firstChoiceX = -visibleGroups.Count * spacing * 0.5f;
            var heal = CreateFeatureChoiceCard(
                "调息回神",
                "休整",
                "恢复 10% - 30% 生命。若持有太极图残卷，恢复幅度还会更高。",
                "静坐疗伤",
                LoadEventUiSprite("event_opportunity_lotus_spring") ?? LoadNodeSprite(MapNodeType.Rest),
                uiRoot.transform,
                new Vector2(0.5f, 0.430f),
                new Vector2(238f, 282f),
                new Vector2(firstChoiceX, 0f),
                new Color(0.12f, 0.28f, 0.22f, 0.96f),
                "10% - 30%");
            heal.onClick.AddListener(() =>
            {
                flow.TakeRestHeal();
                BuildUi();
            });

            var label = CreateText("合成标题", uiRoot.transform, new Vector2(0.5f, 0.595f), new Vector2(1000f, 36f), 19, TextAnchor.MiddleCenter);
            label.text = groups.Count > 0 ? "可合成卡牌" : "当前没有三张同级同名卡牌";
            label.color = new Color(0.62f, 0.96f, 0.90f, 0.96f);

            for (var i = 0; i < visibleGroups.Count; i++)
            {
                var group = visibleGroups[i];
                var card = catalog.FindCard(group.Key);
                var button = CreateCardChoiceCard(
                    card,
                    uiRoot.transform,
                    new Vector2(0.5f, 0.430f),
                    new Vector2(168f, 222f),
                    new Vector2(firstChoiceX + (i + 1) * spacing, 0f),
                    "合成",
                    "三合一升阶",
                    "Lv.+1",
                    $"消耗 3 张同名同级卡\n{card.description}");
                button.onClick.AddListener(() =>
                {
                    flow.UpgradeCardsAtRest(group.Key);
                    BuildUi();
                });
            }
        }

        private void BuildOpportunityPanel()
        {
            var body = CreateText("机遇说明", uiRoot.transform, new Vector2(0.5f, 0.58f), new Vector2(980f, 120f), 24, TextAnchor.MiddleCenter);
            var preview = flow.GenerateOpportunityPreview();
            body.text = $"{preview.title}\n{preview.story}\n收益：{preview.reward}    风险：{preview.risk}";

            var button = CreateButton("揭开机遇", uiRoot.transform, new Vector2(0.5f, 0.42f), new Vector2(280f, 72f));
            button.onClick.AddListener(() =>
            {
                flow.ResolveOpportunity();
                BuildUi();
            });
        }

        private void BuildOpportunityChoicesPanel()
        {
            var info = CreateText("机遇说明", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(980f, 56f), 22, TextAnchor.MiddleCenter);
            info.text = "选择一个机遇处理方式。机遇偏收益，风险通常较小。";
            info.color = new Color(0.86f, 0.94f, 0.92f, 0.94f);

            var stage = CreatePanel("机遇选择舞台", uiRoot.transform, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.43f), Vector2.zero, new Vector2(1060f, 430f), new Color(0f, 0f, 0f, 0.035f));
            stage.raycastTarget = false;
            AddHorizontalOrnament(stage.transform, new Vector2(0f, 214f), 860f, new Color(0.58f, 0.42f, 0.24f, 0.36f));
            AddHorizontalOrnament(stage.transform, new Vector2(0f, -214f), 860f, new Color(0.36f, 0.76f, 0.70f, 0.26f));

            var options = flow.GenerateOpportunityOptions();
            var startX = -((options.Count - 1) * 330f) * 0.5f;
            for (var i = 0; i < options.Count; i++)
            {
                var preview = options[i];
                var button = CreateEventChoiceCard(
                    preview,
                    "机遇",
                    "接受机遇",
                    OpportunityEventArt(preview.templateIndex),
                    uiRoot.transform,
                    new Vector2(0.5f, 0.43f),
                    new Vector2(302f, 386f),
                    new Vector2(startX + i * 330f, 0f),
                    new Color(0.035f, 0.082f, 0.092f, 0.88f),
                    new Color(0.42f, 0.86f, 0.78f, 0.58f));
                button.onClick.AddListener(() =>
                {
                    flow.ChooseOpportunity(preview.templateIndex);
                    BuildUi();
                });
            }
        }

        private void BuildMysteryPanel()
        {
            var info = CreateText("神秘说明", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(1020f, 56f), 22, TextAnchor.MiddleCenter);
            info.text = "神秘房间会给更高收益，也可能带来惩罚战或诅咒。";
            info.color = new Color(0.90f, 0.88f, 0.96f, 0.94f);

            var stage = CreatePanel("神秘选择舞台", uiRoot.transform, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.43f), Vector2.zero, new Vector2(1080f, 440f), new Color(0f, 0f, 0f, 0.050f));
            stage.raycastTarget = false;
            AddHorizontalOrnament(stage.transform, new Vector2(0f, 218f), 880f, new Color(0.62f, 0.32f, 0.72f, 0.34f));
            AddHorizontalOrnament(stage.transform, new Vector2(0f, -218f), 880f, new Color(0.72f, 0.28f, 0.30f, 0.28f));

            var options = flow.GenerateMysteryOptions();
            var startX = -((options.Count - 1) * 338f) * 0.5f;
            for (var i = 0; i < options.Count; i++)
            {
                var preview = options[i];
                var highRisk = preview.templateIndex != 0;
                var button = CreateEventChoiceCard(
                    preview,
                    "神秘",
                    MysteryActionText(preview.templateIndex),
                    MysteryEventArt(preview.templateIndex),
                    uiRoot.transform,
                    new Vector2(0.5f, 0.43f),
                    new Vector2(312f, 398f),
                    new Vector2(startX + i * 338f, 0f),
                    highRisk ? new Color(0.12f, 0.042f, 0.060f, 0.90f) : new Color(0.050f, 0.060f, 0.095f, 0.88f),
                    highRisk ? new Color(0.90f, 0.36f, 0.40f, 0.62f) : new Color(0.54f, 0.72f, 1f, 0.56f));
                button.onClick.AddListener(() =>
                {
                    flow.ChooseMystery(preview.templateIndex);
                    if (!flow.HasPendingNode || flow.HasPendingCardReward)
                    {
                        BuildUi();
                    }
                });
            }
        }

        private void BuildArtifactPanel()
        {
            var titleSprite = LoadArtifactSelectUiSprite("artifact_select_title_art");
            if (titleSprite != null)
            {
                var titleArt = AddSpriteFrame(uiRoot.transform, titleSprite, new Vector2(0f, 248f), new Vector2(500f, 142f));
                titleArt.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                titleArt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                titleArt.color = new Color(1f, 1f, 1f, 0.96f);
            }
            else
            {
                var title = CreateText("强化标题", uiRoot.transform, new Vector2(0.5f, 0.79f), new Vector2(920f, 82f), 54, TextAnchor.MiddleCenter);
                title.text = "选择一个强化";
                title.color = new Color(0.92f, 0.86f, 0.60f, 0.98f);

                var info = CreateText("神器说明", uiRoot.transform, new Vector2(0.5f, 0.715f), new Vector2(1020f, 46f), 22, TextAnchor.MiddleCenter);
                info.text = "强化会立刻加入本局，改变战斗、经济或构筑节奏。";
                info.color = new Color(0.90f, 0.88f, 0.72f, 0.94f);
            }

            var choices = flow.GenerateArtifactChoices();
            var startX = -((choices.Count - 1) * 392f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var artifact = choices[i];
                var button = CreateArtifactChoiceCard(artifact, new Vector2(startX + i * 392f, -42f));
                button.onClick.AddListener(() =>
                {
                    flow.ChooseArtifact(artifact);
                    BuildUi();
                });
            }

            var canRefresh = flow.ArtifactRefreshesRemaining > 0 && flow.CurrentRun.gold >= flow.ArtifactRerollCost;
            var refresh = CreateArtifactSelectButton($"刷新\n{flow.ArtifactRerollCost} 金币", new Vector2(0f, -424f), new Vector2(392f, 94f), "artifact_select_refresh_button", canRefresh);
            refresh.interactable = canRefresh;
            refresh.onClick.AddListener(() =>
            {
                flow.RerollArtifactChoices();
                BuildUi();
            });

            var remainingPanel = CreatePanel("神器刷新次数底", uiRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(470f, -424f), new Vector2(228f, 62f), new Color(0f, 0f, 0f, 0.01f));
            remainingPanel.raycastTarget = false;
            var badgeSprite = LoadArtifactSelectUiSprite("artifact_select_remaining_badge");
            if (badgeSprite != null)
            {
                AddSpriteFrame(remainingPanel.transform, badgeSprite, Vector2.zero, new Vector2(240f, 70f));
            }
            else
            {
                remainingPanel.color = new Color(0.018f, 0.020f, 0.020f, 0.74f);
                remainingPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.78f, 0.58f, 0.34f, 0.55f);
            }

            var remaining = CreateText("刷新次数", remainingPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(194f, 38f), 19, TextAnchor.MiddleCenter);
            remaining.text = $"剩余次数：{flow.ArtifactRefreshesRemaining}";
            remaining.color = new Color(0.94f, 0.84f, 0.58f, 0.96f);
            AddTextShadow(remaining, new Color(0f, 0f, 0f, 0.82f), new Vector2(1f, -1f));
        }

        private void BuildHeader()
        {
            var run = flow.CurrentRun;
            var panel = CreatePanel("顶部信息栏", uiRoot.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(1720f, 72f), new Color(0.015f, 0.018f, 0.022f, 0.80f));
            var headerSprite = LoadArtifactSelectUiSprite("artifact_select_header_frame");
            if (headerSprite != null)
            {
                panel.color = new Color(0f, 0f, 0f, 0.01f);
                AddSpriteFrame(panel.transform, headerSprite, Vector2.zero, new Vector2(1660f, 116f));
            }
            else
            {
                panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.30f, 0.72f, 0.70f, 0.32f);
            }

            var text = CreateText("顶部信息", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(1650f, 58f), 22, TextAnchor.MiddleCenter);
            text.text = $"{GameFlowController.HeroClassName(run.heroClass)}    迷宫 {run.floor}/3 层  房间进度 {run.row}/10    金币 {run.gold}    生命 {run.playerHp:0}/{flow.PlayerMaxHpForRun():0}    本局经验 {run.heroExperience}    主角等级 {flow.CurrentRunPreviewHeroLevel()}    卡组 {run.deckCardIds.Count}    神器 {run.artifactIds.Count}\n{run.lastMessage}";
            text.color = new Color(0.96f, 0.88f, 0.64f, 0.98f);
            AddTextShadow(text, new Color(0f, 0f, 0f, 0.82f), new Vector2(1.2f, -1.2f));
        }

        private void BuildBackdrop(Transform root)
        {
            var isClassSelectBackdrop = titlePanelMode == TitlePanelMode.HeroClassSelect && (flow == null || !flow.HasActiveRun);
            var image = CreatePanel("背景", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.055f, 0.052f, 1f));
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            image.raycastTarget = false;

            var texture = Resources.Load<Texture2D>(CurrentBackdropResourcePath()) ??
                Resources.Load<Texture2D>("Art/AI/Backgrounds/battlefield_honghuang_ai");
            if (texture != null)
            {
                image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                image.preserveAspect = false;
                image.color = isClassSelectBackdrop ? new Color(0.68f, 0.70f, 0.72f, 1f) : new Color(0.56f, 0.58f, 0.60f, 1f);
            }

            var veil = CreatePanel("背景暗雾", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isClassSelectBackdrop ? new Color(0.0f, 0.0f, 0.0f, 0.26f) : new Color(0.0f, 0.0f, 0.0f, 0.22f));
            veil.rectTransform.offsetMin = Vector2.zero;
            veil.rectTransform.offsetMax = Vector2.zero;
            veil.raycastTarget = false;
        }

        private string CurrentBackdropResourcePath()
        {
            if (titlePanelMode == TitlePanelMode.HeroClassSelect && (flow == null || !flow.HasActiveRun))
            {
                return "Art/AI/UI/ClassSelect/class_select_background";
            }

            if (flow != null && flow.HasPendingNode && flow.PendingNode != null)
            {
                return flow.PendingNode.NodeType switch
                {
                    MapNodeType.Artifact => "Art/AI/UI/upgrade_sanctum_bg",
                    MapNodeType.Shop => "Art/AI/UI/upgrade_sanctum_bg",
                    MapNodeType.Rest => "Art/AI/UI/upgrade_sanctum_bg",
                    _ => "Art/AI/UI/map_misty_valley_bg"
                };
            }

            return flow != null && flow.HasActiveRun
                ? "Art/AI/UI/map_misty_valley_bg"
                : "Art/AI/Backgrounds/battlefield_honghuang_ai";
        }

        private void BuildSideMenu()
        {
            var panel = CreatePanel("左侧菜单", uiRoot.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(86f, 520f), new Color(0.010f, 0.016f, 0.018f, 0.48f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.70f, 0.54f, 0.32f, 0.24f);

            var entries = new (string Label, SidePanelMode Mode)[]
            {
                ("路线", SidePanelMode.None),
                ("卡组", SidePanelMode.Deck),
                ("神器", SidePanelMode.Artifacts),
                ("成长", SidePanelMode.Progress),
                ("图鉴", SidePanelMode.Codex),
                ("日志", SidePanelMode.Log)
            };
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var button = CreateButton(entry.Label, panel.transform, new Vector2(0.5f, 1f), new Vector2(68f, 70f), new Vector2(0f, -54f - i * 84f));
                button.GetComponent<Image>().color = sidePanelMode == entry.Mode
                    ? new Color(0.045f, 0.18f, 0.17f, 0.88f)
                    : new Color(0.018f, 0.026f, 0.028f, 0.58f);
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = sidePanelMode == entry.Mode
                    ? new Color(0.66f, 0.88f, 0.76f, 0.42f)
                    : new Color(0.72f, 0.54f, 0.32f, 0.18f);
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontSize = 19;
                    label.resizeTextMaxSize = 19;
                }

                var mode = entry.Mode;
                button.onClick.AddListener(() =>
                {
                    sidePanelMode = mode;
                    BuildUi();
                });
            }
        }

        private void BuildSideDetailPanel()
        {
            if (sidePanelMode == SidePanelMode.None || flow == null || flow.CurrentRun == null)
            {
                return;
            }

            var panel = CreatePanel("侧边详情", uiRoot.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-260f, -12f), new Vector2(430f, 620f), new Color(0.018f, 0.022f, 0.026f, 0.82f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.30f, 0.72f, 0.70f, 0.36f);

            switch (sidePanelMode)
            {
                case SidePanelMode.Deck:
                    BuildDeckSidePanel(panel.transform);
                    break;
                case SidePanelMode.Artifacts:
                    BuildArtifactSidePanel(panel.transform);
                    break;
                case SidePanelMode.Progress:
                    BuildProgressSidePanel(panel.transform);
                    break;
                case SidePanelMode.Codex:
                    BuildCodexSidePanel(panel.transform);
                    break;
                case SidePanelMode.Log:
                    BuildRunLogSidePanel(panel.transform);
                    break;
            }
        }

        private void BuildDeckSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "当前卡组");
            var run = flow.CurrentRun;
            var grouped = run.deckCardIds
                .GroupBy(id => id)
                .Select(group => new { Id = group.Key, Count = group.Count(), Card = catalog.FindCard(group.Key) })
                .Where(entry => entry.Card != null)
                .OrderBy(entry => entry.Card.type)
                .ThenBy(entry => entry.Card.cost)
                .ThenBy(entry => entry.Card.displayName)
                .ToList();

            var typeSummary = grouped
                .GroupBy(entry => CardTypeName(entry.Card.type))
                .Select(group => $"{group.Key} {group.Sum(entry => entry.Count)}")
                .ToList();
            CreateSideText(parent, $"总数 {run.deckCardIds.Count}    可合成 {flow.UpgradableCardGroups().Count}\n{string.Join("  ", typeSummary)}", -70f, 360f, 52f, 18, TextAnchor.MiddleLeft);

            for (var i = 0; i < grouped.Count && i < 13; i++)
            {
                var entry = grouped[i];
                var y = -126f - i * 34f;
                var line = $"{entry.Card.displayName} x{entry.Count}    {CardTypeName(entry.Card.type)}  费 {entry.Card.cost}  Lv.{entry.Card.level}";
                CreateSideText(parent, line, y, 372f, 30f, 17, TextAnchor.MiddleLeft);
            }

            if (grouped.Count > 13)
            {
                CreateSideText(parent, $"还有 {grouped.Count - 13} 种卡未显示", -574f, 372f, 28f, 16, TextAnchor.MiddleCenter);
            }
        }

        private void BuildArtifactSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "本局神器");
            var run = flow.CurrentRun;
            var artifacts = run.artifactIds
                .Select(id => catalog.FindArtifact(id))
                .Where(artifact => artifact != null)
                .ToList();

            if (artifacts.Count == 0)
            {
                CreateSideText(parent, "本局还没有神器。\n优先选择神器房，可以更快形成流派。", -92f, 350f, 72f, 19, TextAnchor.MiddleCenter);
            }

            for (var i = 0; i < artifacts.Count && i < 9; i++)
            {
                var artifact = artifacts[i];
                var y = -88f - i * 54f;
                CreateSideText(parent, $"{artifact.displayName}    {ArtifactRarityName(artifact.rarity)}\n{artifact.description}", y, 372f, 48f, 16, TextAnchor.MiddleLeft);
            }

            var permanentNames = flow.PermanentProgress.permanentArtifactIds
                .Select(id => catalog.FindArtifact(id)?.displayName ?? id)
                .ToList();
            CreateSideText(parent, $"永久神器：{(permanentNames.Count == 0 ? "暂无" : string.Join("、", permanentNames))}", -560f, 372f, 44f, 17, TextAnchor.MiddleLeft);
        }

        private void BuildProgressSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "探索成长");
            var run = flow.CurrentRun;
            var progress = flow.PermanentProgress;
            var text =
                $"当前探索\n" +
                $"职业：{GameFlowController.HeroClassName(run.heroClass)}\n" +
                $"{GameFlowController.HeroClassShortStyle(run.heroClass)}\n" +
                $"层数：{run.floor}/3    房间：{run.row}/10\n" +
                $"金币：{run.gold}    生命：{run.playerHp:0}/{flow.PlayerMaxHpForRun():0}\n" +
                $"本局经验：{run.heroExperience}\n\n" +
                $"永久成长\n" +
                $"探索次数：{progress.totalRuns}\n" +
                $"通关次数：{progress.completedRuns}\n" +
                $"总经验：{progress.totalHeroExperience}\n" +
                $"主角等级：{flow.CurrentRunPreviewHeroLevel()}\n" +
                $"下级经验线：{flow.ExperienceForNextHeroLevel()}";
            CreateSideText(parent, text, -205f, 366f, 308f, 20, TextAnchor.MiddleLeft);

            var affix = $"{flow.CurrentFloorAffixName()}\n{flow.CurrentFloorAffixDescription()}";
            CreateSideText(parent, $"本层词缀\n{affix}", -500f, 366f, 92f, 18, TextAnchor.MiddleLeft);
        }

        private void BuildCodexSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "内容图鉴");
            var cardTypes = catalog.cards
                .Where(card => card != null && card.type != CardType.Curse)
                .GroupBy(card => CardTypeName(card.type))
                .Select(group => $"{group.Key} {group.Count()}")
                .ToList();
            var encounters = catalog.encounters
                .Where(encounter => encounter != null)
                .GroupBy(encounter => GameFlowController.NodeTypeName(encounter.nodeType))
                .Select(group => $"{group.Key} {group.Count()}")
                .ToList();
            var text =
                $"卡牌总数：{catalog.cards.Count(card => card != null)}\n" +
                $"{string.Join("  ", cardTypes)}\n\n" +
                $"神器总数：{catalog.artifacts.Count(artifact => artifact != null)}\n" +
                $"敌人单位：{catalog.units.Count(unit => unit != null && unit.faction == Faction.Enemy)}\n" +
                $"我方单位：{catalog.units.Count(unit => unit != null && unit.faction == Faction.Player)}\n\n" +
                $"遭遇\n{string.Join("  ", encounters)}";
            CreateSideText(parent, text, -170f, 368f, 260f, 19, TextAnchor.MiddleLeft);

            CreateSideText(parent, "提示：当前图鉴先展示内容规模，后续可以扩展为逐张卡牌、逐个怪物的详情页。", -505f, 368f, 96f, 17, TextAnchor.MiddleLeft);
        }

        private void BuildRunLogSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "本局日志");
            var records = flow.RunPlaytestRecords.Reverse().Take(3).ToList();
            if (records.Count > 0)
            {
                CreateSideText(parent, "试玩记录", -82f, 374f, 26f, 17, TextAnchor.MiddleLeft);
                for (var i = 0; i < records.Count; i++)
                {
                    CreateSideText(parent, records[i], -114f - i * 38f, 374f, 34f, 14, TextAnchor.MiddleLeft);
                }
            }

            var logs = flow.RunEventLog.Reverse().Take(13).ToList();
            if (logs.Count == 0)
            {
                CreateSideText(parent, "还没有记录。\n进入房间、战斗结算、购买卡牌和获得神器后会自动写入日志。", -106f, 360f, 96f, 18, TextAnchor.MiddleCenter);
                return;
            }

            for (var i = 0; i < logs.Count; i++)
            {
                var y = records.Count > 0 ? -246f - i * 34f : -82f - i * 38f;
                CreateSideText(parent, logs[i], y, 374f, 32f, 15, TextAnchor.MiddleLeft);
            }
        }

        private void CreateSideTitle(Transform parent, string text)
        {
            var title = CreateText("侧栏标题", parent, new Vector2(0.5f, 1f), new Vector2(360f, 52f), 30, TextAnchor.MiddleCenter);
            title.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            title.text = text;
            title.color = new Color(0.82f, 1f, 0.96f, 0.98f);
        }

        private Text CreateSideText(Transform parent, string content, float y, float width, float height, int fontSize, TextAnchor alignment)
        {
            var text = CreateText("侧栏文字", parent, new Vector2(0.5f, 1f), new Vector2(width, height), fontSize, alignment);
            text.rectTransform.anchoredPosition = new Vector2(0f, y);
            text.text = content;
            text.color = new Color(0.88f, 0.94f, 0.92f, 0.96f);
            return text;
        }

        private Button CreateArtifactChoiceCard(ArtifactDefinition artifact, Vector2 offset)
        {
            var size = new Vector2(342f, 538f);
            var button = CreateButton(string.Empty, uiRoot.transform, new Vector2(0.5f, 0.45f), size, offset);
            var image = button.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            var frame = AddSpriteFrame(button.transform, ArtifactCardFrameSprite(artifact.rarity), Vector2.zero, size + new Vector2(24f, 26f));
            if (frame == null)
            {
                image.color = ArtifactPanelColor(artifact.rarity);
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = ArtifactRarityColor(artifact.rarity);
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }

            var iconHalo = AddNodeHalo(button.transform, true, true);
            iconHalo.rectTransform.sizeDelta = new Vector2(198f, 198f);
            iconHalo.rectTransform.anchoredPosition = new Vector2(0f, 98f);
            iconHalo.color = WithAlpha(ArtifactRarityColor(artifact.rarity), 0.18f);
            iconHalo.transform.SetAsFirstSibling();

            var icon = AddSpriteIcon(button.transform, artifact.icon, new Vector2(0f, 96f), new Vector2(142f, 142f));
            if (icon != null)
            {
                icon.color = Color.white;
            }

            var name = CreateText("神器名", button.transform, new Vector2(0.5f, 0.5f), new Vector2(254f, 54f), 32, TextAnchor.MiddleCenter);
            name.rectTransform.anchoredPosition = new Vector2(0f, -58f);
            name.text = artifact.displayName;
            name.color = new Color(0.96f, 0.92f, 0.74f, 0.98f);
            name.raycastTarget = false;
            AddTextShadow(name, new Color(0f, 0f, 0f, 0.86f), new Vector2(1.2f, -1.2f));

            var desc = CreateText("神器描述", button.transform, new Vector2(0.5f, 0.5f), new Vector2(246f, 90f), 20, TextAnchor.MiddleCenter);
            desc.rectTransform.anchoredPosition = new Vector2(0f, -142f);
            desc.text = artifact.description;
            desc.color = new Color(0.95f, 0.90f, 0.78f, 0.94f);
            desc.raycastTarget = false;

            var tag = CreateText("神器分类", button.transform, new Vector2(0.5f, 0.5f), new Vector2(142f, 28f), 18, TextAnchor.MiddleCenter);
            tag.rectTransform.anchoredPosition = new Vector2(0f, -213f);
            tag.text = ArtifactRarityName(artifact.rarity);
            tag.color = new Color(0.96f, 0.88f, 0.70f, 0.98f);
            tag.raycastTarget = false;
            AddTextShadow(tag, new Color(0f, 0f, 0f, 0.82f), new Vector2(1f, -1f));

            return button;
        }

        private Button CreateArtifactSelectButton(string label, Vector2 offset, Vector2 size, string spriteName, bool active)
        {
            var button = CreateButton(string.Empty, uiRoot.transform, new Vector2(0.5f, 0.5f), size, offset);
            var image = button.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);

            var blankLabel = button.GetComponentInChildren<Text>();
            if (blankLabel != null)
            {
                blankLabel.text = string.Empty;
            }

            var sprite = LoadArtifactSelectUiSprite(spriteName);
            if (sprite != null)
            {
                var frame = AddSpriteFrame(button.transform, sprite, Vector2.zero, size);
                frame.color = active ? Color.white : new Color(0.52f, 0.52f, 0.52f, 0.72f);
            }
            else
            {
                image.color = active ? new Color(0.05f, 0.20f, 0.22f, 0.94f) : new Color(0.08f, 0.08f, 0.08f, 0.70f);
            }

            var text = CreateText("神器按钮文字", button.transform, new Vector2(0.5f, 0.5f), size - new Vector2(52f, 20f), 21, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = active ? new Color(0.96f, 0.90f, 0.66f, 0.98f) : new Color(0.62f, 0.62f, 0.58f, 0.82f);
            text.raycastTarget = false;
            AddTextShadow(text, new Color(0f, 0f, 0f, 0.85f), new Vector2(1.2f, -1.2f));
            return button;
        }

        private Button CreateNodeButton(MapNodeRuntime node, Transform parent, Vector2 anchor, Vector2 offset)
        {
            var button = CreateButton(GameFlowController.NodeTypeName(node.NodeType), parent, anchor, new Vector2(196f, 128f), offset);
            var image = button.GetComponent<Image>();
            image.color = NodeColor(node.NodeType);
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.sizeDelta = new Vector2(160f, 48f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -34f);
                label.fontSize = 20;
            }

            AddSpriteIcon(button.transform, LoadNodeSprite(node.NodeType), new Vector2(0f, 22f), new Vector2(52f, 52f));
            return button;
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

        private static Image AddSpriteIcon(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("AI Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static Image AddSpriteFrame(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("AI UI Frame", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static void AddTextShadow(Text text, Color color, Vector2 distance)
        {
            if (text == null)
            {
                return;
            }

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        private static Sprite LoadNodeSprite(MapNodeType nodeType)
        {
            var path = $"Art/AI/UI/Nodes/{NodeIconName(nodeType)}";
            return LoadResourceSprite(path);
        }

        private static Sprite LoadHeroClassSprite(HeroClassType heroClass)
        {
            var fileName = GameContentFactory.GetHeroClassDefinition(heroClass).spriteName;
            return LoadResourceSprite($"Art/AI/UI/Classes/{fileName}");
        }

        private static Sprite LoadHeroClassCutoutSprite(HeroClassType heroClass)
        {
            var fileName = heroClass switch
            {
                HeroClassType.BorderCommander => "class_select_cutout_border_commander",
                HeroClassType.SpiritSummoner => "class_select_cutout_spirit_summoner",
                HeroClassType.ThunderMage => "class_select_cutout_thunder_mage",
                HeroClassType.TalismanSealer => "class_select_cutout_talisman_sealer",
                _ => null
            };

            return string.IsNullOrEmpty(fileName)
                ? null
                : LoadResourceSprite($"Art/AI/UI/ClassSelect/{fileName}");
        }

        private static Sprite LoadClassSelectUiSprite(string fileName)
        {
            return string.IsNullOrEmpty(fileName)
                ? null
                : LoadResourceSprite($"Art/AI/UI/ClassSelect/{fileName}");
        }

        private static Sprite LoadEventUiSprite(string fileName)
        {
            return string.IsNullOrEmpty(fileName)
                ? null
                : LoadResourceSprite($"Art/AI/UI/Events/{fileName}");
        }

        private static Sprite LoadArtifactSelectUiSprite(string fileName)
        {
            return string.IsNullOrEmpty(fileName)
                ? null
                : LoadResourceSprite($"Art/AI/UI/ArtifactSelect/{fileName}");
        }

        private static Sprite LoadResourceSprite(string path)
        {
            if (cachedResourceSprites.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            cachedResourceSprites[path] = sprite;
            return sprite;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = DefaultFont();
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(string label, Transform parent, Vector2 anchor, Vector2 size)
        {
            return CreateButton(label, parent, anchor, size, Vector2.zero);
        }

        private static Button CreateButton(string label, Transform parent, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            go.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.28f, 0.95f);

            var text = CreateText("文字", go.transform, new Vector2(0.5f, 0.5f), size - new Vector2(18f, 14f), 22, TextAnchor.MiddleCenter);
            text.text = label;
            return go.GetComponent<Button>();
        }

        private static Font DefaultFont()
        {
            cachedFont ??= UiFontProvider.DefaultFont();
            return cachedFont;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }

            AddBestInputModule(eventSystem.gameObject);
        }

        private static void AddBestInputModule(GameObject eventSystem)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                foreach (var module in eventSystem.GetComponents<BaseInputModule>())
                {
                    module.enabled = module.GetType() == inputSystemModule;
                }

                var inputModule = eventSystem.GetComponent(inputSystemModule);
                if (inputModule == null)
                {
                    inputModule = eventSystem.AddComponent(inputSystemModule);
                }

                inputSystemModule.GetMethod("AssignDefaultActions")?.Invoke(inputModule, null);
                return;
            }
#endif

            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            {
                if (module is not StandaloneInputModule)
                {
                    module.enabled = false;
                }
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null)
            {
                standalone = eventSystem.AddComponent<StandaloneInputModule>();
            }

            standalone.enabled = true;
        }

        private static bool IsBattleNode(MapNodeType nodeType)
        {
            return nodeType is MapNodeType.NormalMonster or MapNodeType.EliteMonster or MapNodeType.SmallBoss or MapNodeType.FinalBoss;
        }
    }
}
