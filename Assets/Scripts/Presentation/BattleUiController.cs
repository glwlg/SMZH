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
        private const float RuntimeRefreshInterval = 0.1f;
        private const float CardWidth = 112f;
        private const float CardHeight = 168f;
        private const float HandCardSpacing = 82f;
        private const float HandCancelPaddingX = 78f;
        private const float HandCancelBottomPadding = 58f;
        private const float HandCancelTopPadding = 78f;
        private const float CardArtMinX = 0.115f;
        private const float CardArtMaxX = 0.885f;
        private const float CardArtMinY = 0.305f;
        private const float CardArtMaxY = 0.90f;

        private readonly List<CardView> cardViews = new();
        private BattleController battle;
        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform handRoot;
        private RectTransform dragLayer;
        private Text resourceText;
        private Text moraleText;
        private Text battleInfoText;
        private Text bossNameText;
        private Text bossHpText;
        private Text playerBaseHpText;
        private Text enemySkillText;
        private Text divinePowerText;
        private Text cardPoolCountText;
        private Text usedPileCountText;
        private Text noticeText;
        private Text resultText;
        private Image bossPanel;
        private Image bossHpFill;
        private Image playerBaseHpPanel;
        private Image playerBaseHpFill;
        private Image divineChargeFill;
        private Image enemySkillPanel;
        private Image releasePreviewImage;
        private Button restartButton;
        private Button divinePowerButton;
        private Button debugWinButton;
        private Button debugLoseButton;
        private Button debugGoldButton;
        private Button debugRewardButton;
        private Button debugMoraleButton;
        private Button debugSkipButton;
        private float noticeTimer;
        private static Font cachedFont;
        private static Sprite cachedPileCardFrameSprite;
        private static Sprite cachedPileCardBackSprite;
        private static Sprite cachedCircleSprite;
        private readonly List<PileStackLayer> cardPoolStackLayers = new();
        private readonly List<PileStackLayer> usedPileStackLayers = new();
        private bool refreshRequested;
        private float nextRefreshAllowedAt;

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
            RequestRefresh(true);
        }

        public void RequestRefresh(bool immediate = false)
        {
            refreshRequested = true;
            if (immediate)
            {
                RefreshInternal();
            }
        }

        private void Update()
        {
            TickNotice();
            if (!refreshRequested)
            {
                return;
            }

            if (Time.unscaledTime < nextRefreshAllowedAt)
            {
                return;
            }

            RefreshInternal();
        }

        private void RefreshInternal()
        {
            if (battle == null || resourceText == null || moraleText == null)
            {
                return;
            }

            var deck = battle.Deck;
            var cardPoolCount = deck != null ? deck.CardPool.Count : 0;
            var usedCount = deck != null ? deck.UsedPile.Count : 0;
            var moraleHint = battle.NextCardWillUseMorale
                ? "下张强化"
                : $"{battle.MoralePendingSoldiers}/{battle.MoraleSoldiersPerCharge}";
            var structureDiscountHint = battle.HasPendingStructureDiscount
                ? $"\n流云待命 -{battle.PendingStructureCostReduction}"
                : string.Empty;
            resourceText.text = $"费用 {battle.Mana:0.0}/{battle.MaxMana}\n建筑位 {battle.CurrentCommand}/{battle.MaxCommand}{structureDiscountHint}";
            moraleText.text = $"士气 {battle.MoraleCharges}\n{moraleHint}";
            if (cardPoolCountText != null)
            {
                cardPoolCountText.text = $"卡池\n{cardPoolCount}";
            }

            if (usedPileCountText != null)
            {
                usedPileCountText.text = $"已用\n{usedCount}";
            }

            RefreshPileStack(cardPoolStackLayers, deck != null ? deck.CardPool : null, false);
            RefreshPileStack(usedPileStackLayers, deck != null ? deck.UsedPile : null, true);
            RefreshBossHud();
            RefreshPlayerBaseHud();
            RefreshBattleInfo();
            RefreshDivinePower();
            RenderHand();
            refreshRequested = false;
            nextRefreshAllowedAt = Time.unscaledTime + RuntimeRefreshInterval;
        }

        private void RefreshBossHud()
        {
            if (bossPanel == null || bossNameText == null || bossHpFill == null || bossHpText == null || battle == null)
            {
                return;
            }

            var showBossHud = battle.EnemyObjectiveMaxHp > 1f;
            bossPanel.gameObject.SetActive(showBossHud);
            if (!showBossHud)
            {
                return;
            }

            var hp = Mathf.Max(0f, battle.EnemyObjectiveHp);
            var maxHp = Mathf.Max(1f, battle.EnemyObjectiveMaxHp);
            var ratio = Mathf.Clamp01(hp / maxHp);
            bossNameText.text = battle.IsBossLikeEncounter
                ? $"{battle.EnemyObjectiveLabel}：{battle.EncounterDisplayName}"
                : $"目标：{battle.EncounterDisplayName}";
            bossHpText.text = $"{hp:0}/{maxHp:0}";
            bossHpFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        }

        private void RefreshPlayerBaseHud()
        {
            if (playerBaseHpPanel == null || playerBaseHpFill == null || playerBaseHpText == null || battle == null)
            {
                return;
            }

            playerBaseHpPanel.gameObject.SetActive(true);
            var hp = Mathf.Max(0f, battle.PlayerBaseHp);
            var maxHp = Mathf.Max(1f, battle.PlayerBaseMaxHp);
            var ratio = Mathf.Clamp01(hp / maxHp);
            playerBaseHpFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            playerBaseHpFill.color = Color.Lerp(new Color(0.95f, 0.16f, 0.10f, 0.96f), new Color(0.24f, 0.95f, 0.62f, 0.96f), ratio);
            playerBaseHpText.text = $"\u6211\u65b9\u9635\u5fc3  {hp:0}/{maxHp:0}";
        }

        private void RefreshBattleInfo()
        {
            if (battleInfoText == null || enemySkillText == null || battle == null)
            {
                return;
            }

            battleInfoText.text =
                $"{battle.HeroClassLabel} · {battle.BattleStageLabel}\n" +
                $"{battle.HeroClassStyle}\n" +
                $"房间进度 {battle.CurrentRow}/10\n" +
                $"场上 我方 {battle.PlayerUnitCount}  敌方 {battle.EnemyUnitCount}\n" +
                $"击杀妖怪 {battle.DefeatedEnemyCount}\n" +
                $"坚守阵地，击破{battle.EnemyObjectiveLabel}";

            var showSkillPanel = battle.IsBossLikeEncounter;
            if (enemySkillPanel != null)
            {
                enemySkillPanel.gameObject.SetActive(showSkillPanel);
            }

            enemySkillText.text = showSkillPanel
                ? "首领技能\n" + string.Join("\n", battle.EnemySkillHints)
                : string.Empty;
        }

        private void RefreshDivinePower()
        {
            if (divinePowerButton == null || divinePowerText == null || divineChargeFill == null || battle == null)
            {
                return;
            }

            divinePowerButton.interactable = battle.CanReleaseDivinePower;
            var divinePowerName = battle.DivinePowerName;
            divinePowerText.text = battle.CanReleaseDivinePower
                ? FormatDivinePowerName(divinePowerName)
                : $"{divinePowerName}\n{battle.Mana:0.0}/{battle.DivinePowerCost:0}";
            divineChargeFill.rectTransform.anchorMax = new Vector2(battle.DivinePowerCharge, 1f);
            divineChargeFill.color = battle.CanReleaseDivinePower
                ? new Color(0.40f, 0.88f, 1f, 0.88f)
                : new Color(0.22f, 0.48f, 0.78f, 0.68f);
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
                var buttonText = restartButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = text == "胜利" ? "继续探索" : "返回营地";
                }

                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(() => battle.ContinueAfterResult());
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

            var battlePlane = new Plane(Vector3.forward, Vector3.zero);
            var ray = mainCamera.ScreenPointToRay(screenPosition);
            if (!battlePlane.Raycast(ray, out var enter))
            {
                return Vector3.zero;
            }

            var world = ray.GetPoint(enter);
            world.z = 0f;
            return world;
        }

        public bool IsInHandArea(Vector2 screenPosition)
        {
            if (handRoot == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(handRoot, screenPosition, null, out var localPoint))
            {
                return false;
            }

            var cardCount = battle?.Deck?.Hand?.Count ?? cardViews.Count;
            if (cardCount <= 0)
            {
                return false;
            }

            var cardSpan = CardWidth + Mathf.Max(0, cardCount - 1) * HandCardSpacing;
            var cancelWidth = Mathf.Clamp(cardSpan + HandCancelPaddingX * 2f, 360f, 780f);
            return localPoint.x >= -cancelWidth * 0.5f &&
                   localPoint.x <= cancelWidth * 0.5f &&
                   localPoint.y >= -HandCancelBottomPadding &&
                   localPoint.y <= CardHeight + HandCancelTopPadding;
        }

        public void UpdateReleasePreview(CardDefinition card, Vector2 screenPosition)
        {
            if (battle == null || card == null || IsInHandArea(screenPosition))
            {
                HideReleasePreview();
                return;
            }

            var radius = battle.PreviewRadiusForCard(card);
            if (radius <= 0f)
            {
                HideReleasePreview();
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null || dragLayer == null)
            {
                HideReleasePreview();
                return;
            }

            EnsureReleasePreview();
            var world = ScreenToWorld(screenPosition);
            var centerScreen = (Vector2)mainCamera.WorldToScreenPoint(world);
            var edgeScreen = (Vector2)mainCamera.WorldToScreenPoint(world + Vector3.right * radius);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, centerScreen, null, out var centerLocal) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, edgeScreen, null, out var edgeLocal))
            {
                HideReleasePreview();
                return;
            }

            var isStructurePreview = battle.CardPlacesStructure(card);
            var minDiameter = isStructurePreview ? 16f : 24f;
            var diameter = Mathf.Max(minDiameter, Vector2.Distance(centerLocal, edgeLocal) * 2f);
            var canRelease = battle.CanReleaseCardAt(card, world, out _);
            releasePreviewImage.rectTransform.anchoredPosition = centerLocal;
            releasePreviewImage.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            releasePreviewImage.color = canRelease
                ? isStructurePreview
                    ? new Color(1f, 0.78f, 0.26f, 0.36f)
                    : new Color(0.46f, 0.92f, 1f, 0.34f)
                : new Color(1f, 0.18f, 0.10f, 0.36f);
            releasePreviewImage.gameObject.SetActive(true);
            releasePreviewImage.transform.SetAsFirstSibling();
        }

        public void HideReleasePreview()
        {
            if (releasePreviewImage != null)
            {
                releasePreviewImage.gameObject.SetActive(false);
            }
        }

        private void EnsureReleasePreview()
        {
            if (releasePreviewImage != null)
            {
                return;
            }

            var go = new GameObject("释放范围预览", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(dragLayer, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            releasePreviewImage = go.GetComponent<Image>();
            releasePreviewImage.sprite = CircleSprite();
            releasePreviewImage.raycastTarget = false;
            releasePreviewImage.gameObject.SetActive(false);
        }

        private static string FormatDivinePowerName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length == 4
                ? $"{name[..2]}\n{name[2..]}"
                : name;
        }

        private void BuildDefaultLayout(Transform root)
        {
            BuildBossHud(root);
            BuildBattleInfoHud(root);
            BuildEnemySkillHud(root);
            BuildDivinePowerHud(root);

            var resourcePanel = CreatePanel(
                "资源信息",
                root,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(248f, 56f),
                new Vector2(156f, 62f),
                new Color(0.025f, 0.035f, 0.045f, 0.48f));
            resourceText = CreateHudText("资源状态", resourcePanel.transform, Vector2.zero, Vector2.one, 18);

            var moralePanel = CreatePanel(
                "士气信息",
                root,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(248f, 132f),
                new Vector2(156f, 62f),
                new Color(0.025f, 0.035f, 0.045f, 0.48f));
            moraleText = CreateHudText("士气状态", moralePanel.transform, Vector2.zero, Vector2.one, 18);

            noticeText = CreateText("提示", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), 20);
            noticeText.color = new Color(1f, 0.86f, 0.36f, 0.95f);
            noticeText.gameObject.SetActive(false);

            CreatePileHud(
                "卡池牌堆",
                "卡池",
                root,
                new Vector2(0f, 0f),
                new Vector2(86f, 96f),
                cardPoolStackLayers,
                out cardPoolCountText);
            CreatePileHud(
                "已用牌堆",
                "已用",
                root,
                new Vector2(1f, 0f),
                new Vector2(-86f, 96f),
                usedPileStackLayers,
                out usedPileCountText);

            resultText = CreateText("战斗结果", root, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, 64);
            resultText.color = new Color(1f, 0.92f, 0.35f);
            resultText.gameObject.SetActive(false);

            restartButton = CreateButton("重新开始", root, new Vector2(0.5f, 0.46f), new Vector2(220f, 64f));
            restartButton.gameObject.SetActive(false);

            var debugPanel = CreatePanel(
                "调试区",
                root,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-170f, -96f),
                new Vector2(300f, 136f),
                new Color(0.025f, 0.035f, 0.045f, 0.58f));
            var debugTitle = CreateText("调试标题", debugPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -17f), 15);
            debugTitle.text = "调试";
            debugTitle.rectTransform.sizeDelta = new Vector2(280f, 24f);

            debugWinButton = CreateDebugButton("胜利", debugPanel.transform, -92f, -48f);
            debugWinButton.onClick.AddListener(() => battle?.DebugWinNow());
            debugLoseButton = CreateDebugButton("失败", debugPanel.transform, 0f, -48f);
            debugLoseButton.onClick.AddListener(() => battle?.DebugLoseNow());
            debugGoldButton = CreateDebugButton("+金币", debugPanel.transform, 92f, -48f);
            debugGoldButton.onClick.AddListener(() => battle?.DebugAddGold());
            debugRewardButton = CreateDebugButton("抽奖励", debugPanel.transform, -92f, -92f);
            debugRewardButton.onClick.AddListener(() => battle?.DebugOpenCardReward());
            debugMoraleButton = CreateDebugButton("+士气", debugPanel.transform, 0f, -92f);
            debugMoraleButton.onClick.AddListener(() => battle?.DebugAddMorale());
            debugSkipButton = CreateDebugButton("跳节点", debugPanel.transform, 92f, -92f);
            debugSkipButton.onClick.AddListener(() => battle?.DebugSkipNode());

            var hand = new GameObject("手牌区", typeof(RectTransform));
            hand.transform.SetParent(root, false);
            handRoot = hand.GetComponent<RectTransform>();
            handRoot.anchorMin = new Vector2(0.5f, 0f);
            handRoot.anchorMax = new Vector2(0.5f, 0f);
            handRoot.pivot = new Vector2(0.5f, 0f);
            handRoot.sizeDelta = new Vector2(820f, 210f);
            handRoot.anchoredPosition = new Vector2(0f, 8f);
            BuildPlayerBaseHud(root);

            var drag = new GameObject("拖拽层", typeof(RectTransform));
            drag.transform.SetParent(root, false);
            dragLayer = drag.GetComponent<RectTransform>();
            dragLayer.anchorMin = Vector2.zero;
            dragLayer.anchorMax = Vector2.one;
            dragLayer.pivot = new Vector2(0.5f, 0.5f);
            dragLayer.offsetMin = Vector2.zero;
            dragLayer.offsetMax = Vector2.zero;
        }

        private void BuildPlayerBaseHud(Transform root)
        {
            playerBaseHpPanel = CreatePanel(
                "Player Base Hp",
                root,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 34f),
                new Vector2(500f, 28f),
                new Color(0.015f, 0.030f, 0.030f, 0.72f));
            playerBaseHpPanel.raycastTarget = false;
            playerBaseHpPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.28f, 0.95f, 0.78f, 0.34f);

            var barBack = CreatePanel(
                "Player Base Hp Back",
                playerBaseHpPanel.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.02f, 0.055f, 0.055f, 0.90f));
            barBack.rectTransform.offsetMin = new Vector2(9f, 5f);
            barBack.rectTransform.offsetMax = new Vector2(-9f, -5f);
            barBack.raycastTarget = false;

            playerBaseHpFill = CreatePanel(
                "Player Base Hp Fill",
                barBack.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Color(0.24f, 0.95f, 0.62f, 0.96f));
            playerBaseHpFill.rectTransform.offsetMin = Vector2.zero;
            playerBaseHpFill.rectTransform.offsetMax = Vector2.zero;
            playerBaseHpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            playerBaseHpFill.raycastTarget = false;

            playerBaseHpText = CreateText("Player Base Hp Text", playerBaseHpPanel.transform, Vector2.zero, Vector2.one, Vector2.zero, 16);
            playerBaseHpText.rectTransform.offsetMin = Vector2.zero;
            playerBaseHpText.rectTransform.offsetMax = Vector2.zero;
            playerBaseHpText.rectTransform.sizeDelta = Vector2.zero;
            playerBaseHpText.raycastTarget = false;
            playerBaseHpText.color = new Color(0.94f, 1f, 0.90f, 0.98f);
        }

        private void BuildBossHud(Transform root)
        {
            bossPanel = CreatePanel(
                "首领血条",
                root,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -54f),
                new Vector2(940f, 64f),
                new Color(0.015f, 0.018f, 0.024f, 0.62f));
            bossPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.90f, 0.42f, 0.30f, 0.50f);

            bossNameText = CreateText("首领名称", bossPanel.transform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, 23);
            bossNameText.rectTransform.sizeDelta = new Vector2(860f, 26f);
            bossNameText.color = new Color(1f, 0.92f, 0.72f, 0.98f);

            var barBack = CreatePanel(
                "首领血条底",
                bossPanel.transform,
                new Vector2(0.5f, 0.28f),
                new Vector2(0.5f, 0.28f),
                Vector2.zero,
                new Vector2(820f, 18f),
                new Color(0.10f, 0.02f, 0.018f, 0.86f));
            barBack.raycastTarget = false;

            bossHpFill = CreatePanel("首领血条填充", barBack.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.78f, 0.08f, 0.06f, 0.96f));
            bossHpFill.rectTransform.offsetMin = Vector2.zero;
            bossHpFill.rectTransform.offsetMax = Vector2.zero;
            bossHpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            bossHpFill.raycastTarget = false;

            bossHpText = CreateText("首领血量文字", bossPanel.transform, new Vector2(0.5f, 0.28f), new Vector2(0.5f, 0.28f), Vector2.zero, 17);
            bossHpText.rectTransform.sizeDelta = new Vector2(280f, 24f);
            bossHpText.color = new Color(1f, 0.94f, 0.86f, 0.95f);
        }

        private void BuildBattleInfoHud(Transform root)
        {
            var panel = CreatePanel(
                "战况信息",
                root,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(178f, 318f),
                new Vector2(286f, 152f),
                new Color(0.015f, 0.018f, 0.024f, 0.44f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.78f, 0.66f, 0.46f, 0.26f);
            battleInfoText = CreateHudText("战况文字", panel.transform, Vector2.zero, Vector2.one, 20);
            battleInfoText.alignment = TextAnchor.MiddleLeft;
        }

        private void BuildEnemySkillHud(Transform root)
        {
            enemySkillPanel = CreatePanel(
                "首领技能",
                root,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-170f, 344f),
                new Vector2(286f, 238f),
                new Color(0.015f, 0.018f, 0.024f, 0.46f));
            enemySkillPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.58f, 0.45f, 0.90f, 0.30f);
            enemySkillText = CreateHudText("首领技能文字", enemySkillPanel.transform, Vector2.zero, Vector2.one, 19);
            enemySkillText.alignment = TextAnchor.MiddleLeft;
            enemySkillText.color = new Color(0.96f, 0.92f, 1f, 0.96f);
        }

        private void BuildDivinePowerHud(Transform root)
        {
            divinePowerButton = CreateButton("边境\n战令", root, new Vector2(1f, 0f), new Vector2(142f, 142f));
            divinePowerButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-232f, 258f);
            var image = divinePowerButton.GetComponent<Image>();
            image.sprite = CircleSprite();
            image.color = new Color(0.42f, 0.27f, 0.08f, 0.95f);
            divinePowerButton.onClick.AddListener(() => battle?.TryReleaseDivinePower());

            divinePowerText = divinePowerButton.GetComponentInChildren<Text>();
            if (divinePowerText != null)
            {
                divinePowerText.fontSize = 30;
                divinePowerText.resizeTextMaxSize = 28;
                divinePowerText.resizeTextMinSize = 15;
                divinePowerText.color = new Color(1f, 0.94f, 0.74f, 0.98f);
            }

            var chargeBack = CreatePanel("神通充能底", divinePowerButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(104f, 14f), new Color(0.03f, 0.04f, 0.06f, 0.88f));
            chargeBack.raycastTarget = false;
            divineChargeFill = CreatePanel("神通充能", chargeBack.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.22f, 0.48f, 0.78f, 0.68f));
            divineChargeFill.rectTransform.offsetMin = Vector2.zero;
            divineChargeFill.rectTransform.offsetMax = Vector2.zero;
            divineChargeFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            divineChargeFill.raycastTarget = false;
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

        private static void RefreshPileStack(IReadOnlyList<PileStackLayer> stackLayers, IReadOnlyList<CardDefinition> cards, bool newestOnTop)
        {
            var count = cards != null ? cards.Count : 0;
            for (var i = 0; i < stackLayers.Count; i++)
            {
                var visibleLayer = count > i * 2;
                var layer = stackLayers[i];
                layer.Root.gameObject.SetActive(visibleLayer);
                if (!visibleLayer)
                {
                    continue;
                }

                var previewIndex = newestOnTop
                    ? Mathf.Clamp(count - 1 - i * 2, 0, Mathf.Max(0, count - 1))
                    : Mathf.Clamp(i * 2, 0, Mathf.Max(0, count - 1));
                var previewCard = cards != null && previewIndex >= 0 && previewIndex < count ? cards[previewIndex] : null;
                var showArt = previewCard != null && previewCard.art != null;
                layer.Art.enabled = showArt;
                if (!showArt)
                {
                    continue;
                }

                layer.Art.sprite = previewCard.art;
                FitSpriteToCover(layer.ArtRect, 52f, 78f, previewCard.art);
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

        private static Text CreateHudText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, int size)
        {
            var text = CreateText(name, parent, anchorMin, anchorMax, Vector2.zero, size);
            text.rectTransform.sizeDelta = Vector2.zero;
            text.rectTransform.offsetMin = new Vector2(8f, 4f);
            text.rectTransform.offsetMax = new Vector2(-8f, -4f);
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }

        private static void CreatePileHud(string name, string label, Transform parent, Vector2 anchor, Vector2 position, List<PileStackLayer> stackLayers, out Text countText)
        {
            var panel = CreatePanel(
                name,
                parent,
                anchor,
                anchor,
                position,
                new Vector2(112f, 142f),
                new Color(0.10f, 0.07f, 0.04f, 0.24f));

            var cardFrameSprite = PileCardFrameSprite();
            for (var i = 0; i < 4; i++)
            {
                var go = new GameObject($"{label}卡背{i + 1}", typeof(RectTransform), typeof(Image), typeof(Outline));
                go.transform.SetParent(panel.transform, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(62f, 88f);
                rect.anchoredPosition = new Vector2(-7f + i * 5f, 12f - i * 3f);
                rect.localRotation = Quaternion.Euler(0f, 0f, -7f + i * 4.5f);

                var image = go.GetComponent<Image>();
                image.sprite = PileCardBackSprite();
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = new Color(0.24f, 0.16f, 0.09f, 0.92f);
                image.raycastTarget = false;

                var artMaskGo = new GameObject("牌堆卡图框", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                artMaskGo.transform.SetParent(go.transform, false);
                var artMaskRect = artMaskGo.GetComponent<RectTransform>();
                artMaskRect.anchorMin = new Vector2(0.08f, 0.08f);
                artMaskRect.anchorMax = new Vector2(0.92f, 0.92f);
                artMaskRect.offsetMin = Vector2.zero;
                artMaskRect.offsetMax = Vector2.zero;
                var artMaskImage = artMaskGo.GetComponent<Image>();
                artMaskImage.color = new Color(0.10f, 0.06f, 0.04f, 0.22f);
                artMaskImage.raycastTarget = false;

                var artGo = new GameObject("牌堆卡图", typeof(RectTransform), typeof(Image));
                artGo.transform.SetParent(artMaskGo.transform, false);
                var artRect = artGo.GetComponent<RectTransform>();
                artRect.anchorMin = new Vector2(0.5f, 0.5f);
                artRect.anchorMax = new Vector2(0.5f, 0.5f);
                artRect.pivot = new Vector2(0.5f, 0.5f);
                var artImage = artGo.GetComponent<Image>();
                artImage.color = Color.white;
                artImage.raycastTarget = false;
                artImage.enabled = false;

                var frameGo = new GameObject("牌堆牌框", typeof(RectTransform), typeof(Image));
                frameGo.transform.SetParent(go.transform, false);
                var frameRect = frameGo.GetComponent<RectTransform>();
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                var frameImage = frameGo.GetComponent<Image>();
                frameImage.sprite = cardFrameSprite;
                frameImage.color = cardFrameSprite != null ? Color.white : new Color(0.88f, 0.66f, 0.30f, 0.82f);
                frameImage.raycastTarget = false;

                var outline = go.GetComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.025f, 0.01f, 0.68f);
                outline.effectDistance = new Vector2(2f, -2f);
                stackLayers.Add(new PileStackLayer(go.GetComponent<RectTransform>(), artImage, artRect));
            }

            countText = CreateText($"{label}数量", panel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), 18);
            countText.rectTransform.sizeDelta = new Vector2(92f, 48f);
            countText.color = new Color(1f, 0.90f, 0.58f, 0.96f);
            countText.alignment = TextAnchor.MiddleCenter;
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

        private static Button CreateDebugButton(string label, Transform parent, float x, float y)
        {
            var button = CreateButton(label, parent, new Vector2(0.5f, 1f), new Vector2(82f, 34f));
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 16;
                text.resizeTextMaxSize = 16;
            }

            return button;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
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

                var module = eventSystem.GetComponent(inputSystemModule);
                if (module == null)
                {
                    module = eventSystem.AddComponent(inputSystemModule);
                }

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

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null)
            {
                standalone = eventSystem.AddComponent<StandaloneInputModule>();
            }

            standalone.enabled = true;
        }

        private static Font DefaultFont()
        {
            cachedFont ??= UiFontProvider.DefaultFont();
            return cachedFont;
        }

        private static Sprite PileCardFrameSprite()
        {
            if (cachedPileCardFrameSprite != null)
            {
                return cachedPileCardFrameSprite;
            }

            cachedPileCardFrameSprite = Resources.Load<Sprite>("Art/AI/UI/card_frame_honghuang");
            if (cachedPileCardFrameSprite != null)
            {
                return cachedPileCardFrameSprite;
            }

            var texture = Resources.Load<Texture2D>("Art/AI/UI/card_frame_honghuang");
            if (texture == null)
            {
                return null;
            }

            cachedPileCardFrameSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            return cachedPileCardFrameSprite;
        }

        private static Sprite PileCardBackSprite()
        {
            if (cachedPileCardBackSprite != null)
            {
                return cachedPileCardBackSprite;
            }

            cachedPileCardBackSprite = Resources.Load<Sprite>("Art/AI/Cards/card_reward_back");
            if (cachedPileCardBackSprite != null)
            {
                return cachedPileCardBackSprite;
            }

            var texture = Resources.Load<Texture2D>("Art/AI/Cards/card_reward_back");
            if (texture == null)
            {
                return null;
            }

            cachedPileCardBackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            return cachedPileCardBackSprite;
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
                name = "XTD_BattleCircle"
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
                    var alpha = Mathf.Clamp01((1f - distance) * 10f);
                    var rim = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.82f) * 18f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(alpha * 0.78f, rim * 0.58f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return cachedCircleSprite;
        }

        private static void FitSpriteToCover(RectTransform targetRect, float frameWidth, float frameHeight, Sprite sprite)
        {
            if (targetRect == null)
            {
                return;
            }

            if (sprite == null)
            {
                targetRect.sizeDelta = new Vector2(frameWidth, frameHeight);
                targetRect.anchoredPosition = Vector2.zero;
                targetRect.localRotation = Quaternion.identity;
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

            targetRect.sizeDelta = new Vector2(width, height);
            targetRect.anchoredPosition = Vector2.zero;
            targetRect.localRotation = Quaternion.identity;
        }

        private sealed class PileStackLayer
        {
            public PileStackLayer(RectTransform root, Image art, RectTransform artRect)
            {
                Root = root;
                Art = art;
                ArtRect = artRect;
            }

            public RectTransform Root { get; }
            public Image Art { get; }
            public RectTransform ArtRect { get; }
        }

        private sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private BattleUiController owner;
            private BattleController battle;
            private CardDefinition card;
            private RectTransform rect;
            private RectTransform visualRoot;
            private CanvasGroup canvasGroup;
            private Image frame;
            private Image innerPanel;
            private Image artFrameImage;
            private Image icon;
            private Image typeBand;
            private Image costBgImage;
            private RectTransform iconRect;
            private Text title;
            private Text cost;
            private Text description;
            private bool hovered;
            private bool dragging;
            private bool canPlay;
            private string blockReason = string.Empty;
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
                var hitArea = go.GetComponent<Image>();
                hitArea.color = new Color(1f, 1f, 1f, 0f);
                hitArea.raycastTarget = true;

                view.BuildVisuals(font);
                return view;
            }

            public void Bind(BattleUiController ownerController, BattleController battleController, CardDefinition definition, int index, int count, bool playable)
            {
                owner = ownerController;
                battle = battleController;
                card = definition;
                canPlay = playable;
                blockReason = playable ? string.Empty : battleController.CardBlockReason(definition);

                title.text = definition.displayName;
                cost.text = battleController.EffectiveCardCost(definition).ToString();
                description.text = !playable && !string.IsNullOrWhiteSpace(blockReason)
                    ? blockReason
                    : definition.CanReceiveMorale && battleController.NextCardWillUseMorale
                    ? $"{CardTypeLabel(definition)}\n士气待发"
                    : CardTypeLabel(definition);
                icon.sprite = definition.art;
                icon.enabled = definition.art != null;
                icon.preserveAspect = false;
                FitArtToCover(definition.art);

                var playableColor = CardColor(definition.type);
                frame.color = frame.sprite != null
                    ? (playable ? Color.white : new Color(0.48f, 0.48f, 0.50f, 0.82f))
                    : (playable ? playableColor : new Color(0.07f, 0.07f, 0.08f, 0.82f));
                if (innerPanel != null)
                {
                    innerPanel.color = playable ? WithAlpha(playableColor, 0.44f) : new Color(0.07f, 0.07f, 0.08f, 0.60f);
                }

                if (artFrameImage != null)
                {
                    artFrameImage.color = playable ? new Color(0.07f, 0.05f, 0.04f, 0.58f) : new Color(0.05f, 0.05f, 0.06f, 0.66f);
                }

                if (typeBand != null)
                {
                    typeBand.color = playable ? CardInfoPanelColor(definition.type) : new Color(0.11f, 0.10f, 0.09f, 0.78f);
                }

                title.color = playable ? new Color(1f, 0.93f, 0.66f, 0.99f) : new Color(0.68f, 0.68f, 0.70f, 0.92f);
                description.color = playable ? new Color(0.96f, 0.99f, 0.86f, 0.98f) : new Color(0.62f, 0.62f, 0.66f, 0.90f);

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
                    owner.ShowNotice(string.IsNullOrWhiteSpace(blockReason) ? "费用或建筑位不足" : blockReason);
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
                owner.UpdateReleasePreview(card, eventData.position);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (!dragging)
                {
                    return;
                }

                MoveToPointer(eventData.position);
                owner.UpdateReleasePreview(card, eventData.position);
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
                owner.HideReleasePreview();

                if (owner.IsInHandArea(eventData.position))
                {
                    owner.ShowNotice("已取消释放", 0.65f);
                    owner.Refresh();
                    return;
                }

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
                var visualGo = new GameObject("卡牌整体视觉", typeof(RectTransform));
                visualGo.transform.SetParent(transform, false);
                visualRoot = visualGo.GetComponent<RectTransform>();
                visualRoot.anchorMin = Vector2.zero;
                visualRoot.anchorMax = Vector2.one;
                visualRoot.offsetMin = Vector2.zero;
                visualRoot.offsetMax = Vector2.zero;
                visualRoot.pivot = new Vector2(0.5f, 0.5f);
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;

                var inner = new GameObject("Card Interior", typeof(RectTransform), typeof(Image));
                inner.transform.SetParent(visualRoot, false);
                var innerRect = inner.GetComponent<RectTransform>();
                innerRect.anchorMin = new Vector2(0.08f, 0.08f);
                innerRect.anchorMax = new Vector2(0.92f, 0.91f);
                innerRect.offsetMin = Vector2.zero;
                innerRect.offsetMax = Vector2.zero;
                innerPanel = inner.GetComponent<Image>();
                innerPanel.color = new Color(0.16f, 0.08f, 0.05f, 0.64f);
                innerPanel.raycastTarget = false;

                var artFrame = new GameObject("卡图裁剪", typeof(RectTransform), typeof(Image), typeof(Mask));
                artFrame.transform.SetParent(visualRoot, false);
                var artFrameRect = artFrame.GetComponent<RectTransform>();
                artFrameRect.anchorMin = new Vector2(CardArtMinX, CardArtMinY);
                artFrameRect.anchorMax = new Vector2(CardArtMaxX, CardArtMaxY);
                artFrameRect.offsetMin = Vector2.zero;
                artFrameRect.offsetMax = Vector2.zero;
                artFrameImage = artFrame.GetComponent<Image>();
                artFrameImage.color = new Color(0.08f, 0.06f, 0.05f, 0.44f);
                artFrameImage.raycastTarget = false;
                var artMask = artFrame.GetComponent<Mask>();
                artMask.showMaskGraphic = false;

                var iconGo = new GameObject("图标", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(artFrame.transform, false);
                iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = Vector2.zero;
                icon = iconGo.GetComponent<Image>();
                icon.color = Color.white;
                icon.raycastTarget = false;

                var typeBandGo = new GameObject("Card Type Band", typeof(RectTransform), typeof(Image));
                typeBandGo.transform.SetParent(visualRoot, false);
                var typeBandRect = typeBandGo.GetComponent<RectTransform>();
                typeBandRect.anchorMin = new Vector2(0.105f, 0.055f);
                typeBandRect.anchorMax = new Vector2(0.895f, 0.300f);
                typeBandRect.offsetMin = Vector2.zero;
                typeBandRect.offsetMax = Vector2.zero;
                typeBand = typeBandGo.GetComponent<Image>();
                typeBand.color = new Color(0.42f, 0.18f, 0.09f, 0.86f);
                typeBand.raycastTarget = false;

                var frameGo = new GameObject("牌框覆盖", typeof(RectTransform), typeof(Image));
                frameGo.transform.SetParent(visualRoot, false);
                var frameRect = frameGo.GetComponent<RectTransform>();
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                frame = frameGo.GetComponent<Image>();
                frame.sprite = CardFrameSprite();
                frame.type = Image.Type.Simple;
                frame.preserveAspect = false;
                frame.color = frame.sprite != null ? Color.white : new Color(0.20f, 0.10f, 0.08f, 0.97f);
                frame.raycastTarget = false;

                title = CreateCardText("标题", visualRoot, font, new Vector2(0.115f, 0.168f), new Vector2(0.885f, 0.292f), 16, TextAnchor.MiddleCenter);
                description = CreateCardText("类型", visualRoot, font, new Vector2(0.115f, 0.066f), new Vector2(0.885f, 0.170f), 13, TextAnchor.MiddleCenter);
                AddCardTextOutline(title, new Color(0.12f, 0.04f, 0.015f, 0.92f), new Vector2(1.1f, -1.1f));
                AddCardTextOutline(description, new Color(0.08f, 0.035f, 0.012f, 0.92f), new Vector2(1f, -1f));

                var costBg = new GameObject("费用底", typeof(RectTransform), typeof(Image));
                costBg.transform.SetParent(visualRoot, false);
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

            private void FitArtToCover(Sprite sprite)
            {
                if (iconRect == null)
                {
                    return;
                }

                var frameWidth = CardWidth * (CardArtMaxX - CardArtMinX);
                var frameHeight = CardHeight * (CardArtMaxY - CardArtMinY);
                FitSpriteToCover(iconRect, frameWidth, frameHeight, sprite);
            }

            private void SetHome(int index, int count)
            {
                var center = (count - 1) * 0.5f;
                var offset = index - center;
                var normalized = count <= 1 ? 0f : offset / center;

                homePosition = new Vector2(offset * HandCardSpacing, Mathf.Abs(normalized) * -18f);
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
                if (definition.type == CardType.Curse)
                {
                    return "诅咒";
                }

                var moraleEffect = definition.effects.Find(effect => effect != null && effect.effectType == EffectType.GainMorale);
                if (moraleEffect != null)
                {
                    return $"士气 +{Mathf.Max(1, Mathf.RoundToInt(moraleEffect.value))}";
                }

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
                if (type == CardType.Curse)
                {
                    return new Color(0.12f, 0.05f, 0.13f, 0.97f);
                }

                return type switch
                {
                    CardType.Structure => new Color(0.35f, 0.18f, 0.10f, 0.97f),
                    CardType.Spell => new Color(0.30f, 0.09f, 0.10f, 0.97f),
                    CardType.Tactic => new Color(0.16f, 0.20f, 0.12f, 0.97f),
                    CardType.EliteSoldier or CardType.Hero => new Color(0.20f, 0.16f, 0.08f, 0.97f),
                    _ => new Color(0.12f, 0.16f, 0.18f, 0.97f)
                };
            }

            private static Color CardInfoPanelColor(CardType type)
            {
                return type switch
                {
                    CardType.Structure => new Color(0.48f, 0.22f, 0.11f, 0.90f),
                    CardType.Spell => new Color(0.50f, 0.16f, 0.13f, 0.90f),
                    CardType.Tactic => new Color(0.34f, 0.31f, 0.14f, 0.90f),
                    CardType.EliteSoldier or CardType.Hero => new Color(0.46f, 0.30f, 0.12f, 0.90f),
                    CardType.Curse => new Color(0.24f, 0.12f, 0.25f, 0.90f),
                    _ => new Color(0.32f, 0.25f, 0.17f, 0.90f)
                };
            }

            private static Color WithAlpha(Color color, float alpha)
            {
                color.a = alpha;
                return color;
            }

            private static void AddCardTextOutline(Text text, Color color, Vector2 distance)
            {
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = color;
                outline.effectDistance = distance;
            }

            private static Sprite CardFrameSprite()
            {
                if (cachedCardFrameSprite != null)
                {
                    return cachedCardFrameSprite;
                }

                cachedCardFrameSprite = Resources.Load<Sprite>("Art/AI/UI/card_frame_honghuang");
                if (cachedCardFrameSprite != null)
                {
                    return cachedCardFrameSprite;
                }

                var texture = Resources.Load<Texture2D>("Art/AI/UI/card_frame_honghuang");
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
