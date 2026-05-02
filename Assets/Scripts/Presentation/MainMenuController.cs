using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        private GameFlowController flow;
        private GameObject uiRoot;

        private void Start()
        {
            catalog ??= DemoContentFactory.CreateCatalog();
            DemoContentFactory.EnsureCatalogComplete(catalog);
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
            var title = CreateText("标题", uiRoot.transform, new Vector2(0.5f, 0.67f), new Vector2(860f, 110f), 56, TextAnchor.MiddleCenter);
            title.text = "X-TD 洪荒边境";

            var subtitle = CreateText("副标题", uiRoot.transform, new Vector2(0.5f, 0.58f), new Vector2(920f, 68f), 24, TextAnchor.MiddleCenter);
            subtitle.text = "肉鸽爬塔 · 正面对抗 · 卡牌阵地";
            subtitle.color = new Color(1f, 0.86f, 0.48f, 0.95f);

            var start = CreateButton("开始探索", uiRoot.transform, new Vector2(0.5f, 0.43f), new Vector2(320f, 76f));
            start.onClick.AddListener(() => flow.StartNewRun(catalog));

            var battle = CreateButton("只看战斗原型", uiRoot.transform, new Vector2(0.5f, 0.33f), new Vector2(320f, 66f));
            battle.onClick.AddListener(() => SceneManager.LoadScene("BattlePrototype"));

            var quit = CreateButton("退出", uiRoot.transform, new Vector2(0.5f, 0.24f), new Vector2(320f, 62f));
            quit.onClick.AddListener(Application.Quit);
        }

        private void BuildRunEnd(string titleText, string body)
        {
            BuildHeader();
            var title = CreateText("结算标题", uiRoot.transform, new Vector2(0.5f, 0.64f), new Vector2(760f, 96f), 48, TextAnchor.MiddleCenter);
            title.text = titleText;

            var summary = CreateText("结算内容", uiRoot.transform, new Vector2(0.5f, 0.52f), new Vector2(980f, 128f), 24, TextAnchor.MiddleCenter);
            var run = flow.CurrentRun;
            summary.text = $"{body}\n金币 {run.gold}    主角经验 {run.heroExperience}    本局神器 {run.artifactIds.Count}    永久神器 {run.permanentArtifactIds.Count}";

            var restart = CreateButton("重新开始探索", uiRoot.transform, new Vector2(0.5f, 0.36f), new Vector2(320f, 76f));
            restart.onClick.AddListener(() => flow.StartNewRun(catalog));

            var menu = CreateButton("返回标题", uiRoot.transform, new Vector2(0.5f, 0.26f), new Vector2(280f, 62f));
            menu.onClick.AddListener(() => flow.ReturnToTitle());
        }

        private void BuildMapPanel()
        {
            BuildHeader();

            var title = CreateText("迷宫标题", uiRoot.transform, new Vector2(0.5f, 0.84f), new Vector2(920f, 54f), 32, TextAnchor.MiddleCenter);
            title.text = $"迷宫第 {flow.CurrentRun.floor} 层路线";

            var currentChoices = flow.CurrentChoices().Select(node => node.Key).ToHashSet();
            var selectedNodes = flow.CurrentRun.selectedNodeKeys.ToHashSet();
            var floorRows = flow.MapRows
                .Where(row => row.Count > 0 && row[0].Floor == flow.CurrentRun.floor)
                .OrderBy(row => row[0].Row)
                .ToList();

            var mapPanel = CreatePanel("层路线图", uiRoot.transform, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(1180f, 620f), new Color(0.02f, 0.03f, 0.035f, 0.28f));
            var mapRoot = mapPanel.rectTransform;
            var previewText = CreateText("房间预览", uiRoot.transform, new Vector2(0.82f, 0.80f), new Vector2(430f, 112f), 18, TextAnchor.MiddleLeft);
            previewText.text = "把鼠标移到房间上，可以预览类型、奖励和风险。";
            previewText.color = new Color(0.92f, 0.94f, 0.88f, 0.94f);
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
                            ? new Color(1f, 0.76f, 0.24f, 0.92f)
                            : activeLine
                                ? new Color(0.62f, 0.86f, 1f, 0.62f)
                                : new Color(0.50f, 0.56f, 0.58f, 0.30f);
                        CreateMapLine(mapRoot, from, to, color, selectedLine ? 5f : 3f);
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
                        if (!IsBattleNode(selectedNode.NodeType) && selectedNode.NodeType != MapNodeType.Mystery)
                        {
                            BuildUi();
                        }
                    });
                }
            }

            var progress = CreateText("路线说明", uiRoot.transform, new Vector2(0.5f, 0.12f), new Vector2(1080f, 72f), 20, TextAnchor.MiddleCenter);
            progress.text = "从下往上选择房间。亮起的房间可以进入，金色房间是已走路径；连线决定下一步可选路线。";
            progress.color = new Color(0.90f, 0.92f, 0.88f, 0.92f);
        }

        private static Vector2 NodeMapPosition(IReadOnlyList<MapNodeRuntime> row, MapNodeRuntime node)
        {
            var center = (row.Count - 1) * 0.5f;
            var x = (node.NodeIndex - center) * 245f;
            var y = -270f + (node.Row - 1) * 60f;
            return new Vector2(x, y);
        }

        private Button CreateMapRoomButton(MapNodeRuntime node, Transform parent, Vector2 offset, bool available, bool selected)
        {
            var button = CreateButton(NodeShortName(node.NodeType), parent, new Vector2(0.5f, 0.5f), new Vector2(132f, 56f), offset);
            var image = button.GetComponent<Image>();
            image.color = selected
                ? new Color(0.58f, 0.39f, 0.10f, 0.98f)
                : available
                    ? NodeColor(node.NodeType)
                    : new Color(0.10f, 0.12f, 0.13f, 0.72f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                var labelRect = label.rectTransform;
                labelRect.sizeDelta = new Vector2(72f, 42f);
                labelRect.anchoredPosition = new Vector2(24f, 0f);
                label.fontSize = 18;
            }

            AddSpriteIcon(button.transform, LoadNodeSprite(node.NodeType), new Vector2(-42f, 0f), new Vector2(42f, 42f));

            if (selected || available)
            {
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = selected ? new Color(1f, 0.82f, 0.32f, 0.98f) : new Color(0.78f, 0.92f, 1f, 0.78f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            return button;
        }

        private void AddNodePreview(Button button, MapNodeRuntime node, Text previewText, bool reachable)
        {
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => previewText.text = BuildNodePreview(node, reachable));
            AddTrigger(trigger, EventTriggerType.PointerClick, () => previewText.text = BuildNodePreview(node, reachable));
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
        }

        private static string BuildNodePreview(MapNodeRuntime node, bool reachable)
        {
            var state = reachable ? "当前路线可达" : "暂未连通";
            return node.NodeType switch
            {
                MapNodeType.NormalMonster => $"普通怪物\n{state}\n奖励：金币\n目标：摧毁敌方基地。",
                MapNodeType.EliteMonster => $"精英怪物\n{state}\n奖励：金币，从 3 张卡中选 1 张\n提示：敌方核心站桩输出并派兵。",
                MapNodeType.Shop => $"商店\n{state}\n奖励：构筑调整\n提示：购买新牌，或半价出售已有牌。",
                MapNodeType.Rest => $"休息\n{state}\n奖励：回血或三合一合成\n提示：合成最多升到 3 级。",
                MapNodeType.Opportunity => $"机遇\n{state}\n奖励：事件收益\n提示：可能获得金币、卡牌、升级或承担小风险。",
                MapNodeType.Mystery => $"神秘\n{state}\n奖励/风险：高收益或凶阵战斗\n提示：凶阵打赢也没有额外奖励。",
                MapNodeType.Artifact => $"神器层\n{state}\n奖励：3 个神器选 1 个\n提示：观星镜可提高可选数量。",
                MapNodeType.SmallBoss => $"小首领\n{state}\n奖励：金币、经验，从 4 张卡中选 2 张\n提示：核心生命低时技能节奏会加快。",
                MapNodeType.FinalBoss => $"最终首领\n{state}\n奖励：大量金币、经验、永久神器，从 5 张卡中选 3 张\n提示：击败后结束本次探索。",
                _ => $"房间\n{state}\n奖励：未知"
            };
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
                MapNodeType.NormalMonster => new Color(0.19f, 0.24f, 0.22f, 0.96f),
                MapNodeType.EliteMonster => new Color(0.36f, 0.17f, 0.13f, 0.96f),
                MapNodeType.Shop => new Color(0.28f, 0.21f, 0.08f, 0.96f),
                MapNodeType.Rest => new Color(0.12f, 0.28f, 0.22f, 0.96f),
                MapNodeType.Opportunity => new Color(0.22f, 0.20f, 0.36f, 0.96f),
                MapNodeType.Mystery => new Color(0.16f, 0.13f, 0.22f, 0.96f),
                MapNodeType.Artifact => new Color(0.31f, 0.20f, 0.08f, 0.96f),
                MapNodeType.SmallBoss or MapNodeType.FinalBoss => new Color(0.42f, 0.08f, 0.08f, 0.96f),
                _ => new Color(0.15f, 0.2f, 0.28f, 0.95f)
            };
        }

        private static string CardTypeName(CardType type)
        {
            return type switch
            {
                CardType.Structure => "建筑",
                CardType.Soldier => "士兵",
                CardType.EliteSoldier => "精兵",
                CardType.Hero => "英雄",
                CardType.Spell => "法术",
                CardType.Tactic => "战术",
                _ => "卡牌"
            };
        }

        private static Color CardPanelColor(CardType type)
        {
            return type switch
            {
                CardType.Structure => new Color(0.33f, 0.18f, 0.10f, 0.96f),
                CardType.Spell => new Color(0.34f, 0.09f, 0.08f, 0.96f),
                CardType.Tactic => new Color(0.14f, 0.22f, 0.14f, 0.96f),
                CardType.EliteSoldier or CardType.Hero => new Color(0.30f, 0.22f, 0.08f, 0.96f),
                _ => new Color(0.13f, 0.20f, 0.23f, 0.96f)
            };
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
                    if (!IsBattleNode(node.NodeType) && node.NodeType != MapNodeType.Mystery)
                    {
                        BuildUi();
                    }
                });
            }
        }

        private void BuildNodePanel(MapNodeRuntime node)
        {
            BuildHeader();

            var title = CreateText("节点标题", uiRoot.transform, new Vector2(0.5f, 0.77f), new Vector2(900f, 72f), 36, TextAnchor.MiddleCenter);
            title.text = $"{GameFlowController.NodeTypeName(node.NodeType)}";

            switch (node.NodeType)
            {
                case MapNodeType.Shop:
                    BuildShopPanel();
                    break;
                case MapNodeType.Rest:
                    BuildRestPanel();
                    break;
                case MapNodeType.Opportunity:
                    BuildOpportunityPanel();
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

            var choices = flow.PendingCardRewardChoices();
            var startX = -((choices.Count - 1) * 230f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var card = choices[i];
                var label = $"{card.displayName}\n{CardTypeName(card.type)}  费用 {card.cost}  Lv.{card.level}\n{card.description}";
                var button = CreateButton(label, uiRoot.transform, new Vector2(0.5f, 0.50f), new Vector2(220f, 190f), new Vector2(startX + i * 230f, 0f));
                button.GetComponent<Image>().color = CardPanelColor(card.type);
                button.onClick.AddListener(() =>
                {
                    flow.ChooseCardReward(card);
                    BuildUi();
                });
            }

            var skip = CreateButton("放弃剩余奖励", uiRoot.transform, new Vector2(0.5f, 0.24f), new Vector2(260f, 62f));
            skip.onClick.AddListener(() =>
            {
                flow.SkipCardReward();
                BuildUi();
            });
        }

        private void BuildShopPanel()
        {
            var info = CreateText("商店说明", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(1000f, 52f), 22, TextAnchor.MiddleCenter);
            info.text = $"当前金币 {flow.CurrentRun.gold}。上方购买新卡牌，下方半价出售已有卡牌；离开后进入下一行。";

            var shopCards = flow.GenerateShopCards();
            for (var i = 0; i < shopCards.Count; i++)
            {
                var card = shopCards[i];
                var button = CreateButton($"{card.displayName}\n{CardTypeName(card.type)} Lv.{card.level}\n买 {flow.CardBuyPrice(card)} 金币", uiRoot.transform, new Vector2(0.5f, 0.56f), new Vector2(190f, 108f), new Vector2(-390f + i * 195f, 0f));
                button.onClick.AddListener(() =>
                {
                    flow.BuyCard(card);
                    BuildUi();
                });
            }

            var sellTitle = CreateText("出售标题", uiRoot.transform, new Vector2(0.5f, 0.43f), new Vector2(1000f, 42f), 20, TextAnchor.MiddleCenter);
            sellTitle.text = "出售已有卡牌";
            var sellCards = flow.CurrentRun.deckCardIds
                .GroupBy(id => id)
                .Select(group => new { Id = group.Key, Count = group.Count(), Card = catalog.FindCard(group.Key) })
                .Where(entry => entry.Card != null)
                .Take(8)
                .ToList();
            for (var i = 0; i < sellCards.Count; i++)
            {
                var entry = sellCards[i];
                var button = CreateButton($"{entry.Card.displayName} x{entry.Count}\n卖 {Mathf.Max(1, flow.CardBuyPrice(entry.Card) / 2)}", uiRoot.transform, new Vector2(0.5f, 0.34f), new Vector2(178f, 74f), new Vector2(-620f + i * 178f, 0f));
                button.onClick.AddListener(() =>
                {
                    flow.SellCard(entry.Id);
                    BuildUi();
                });
            }

            var leave = CreateButton("离开商店", uiRoot.transform, new Vector2(0.5f, 0.19f), new Vector2(260f, 66f));
            leave.onClick.AddListener(() =>
            {
                flow.LeaveShop();
                BuildUi();
            });
        }

        private void BuildRestPanel()
        {
            var info = CreateText("休息说明", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(1000f, 52f), 22, TextAnchor.MiddleCenter);
            info.text = "选择回血，或三张同级同名卡牌合成一张高一级卡牌。每次休息只能做一件事。";

            var heal = CreateButton("随机回血 10%-30%", uiRoot.transform, new Vector2(0.5f, 0.57f), new Vector2(300f, 74f));
            heal.onClick.AddListener(() =>
            {
                flow.TakeRestHeal();
                BuildUi();
            });

            var groups = flow.UpgradableCardGroups().ToList();
            var label = CreateText("合成标题", uiRoot.transform, new Vector2(0.5f, 0.46f), new Vector2(1000f, 42f), 20, TextAnchor.MiddleCenter);
            label.text = groups.Count > 0 ? "可合成卡牌" : "当前没有三张同级同名卡牌";

            for (var i = 0; i < groups.Count && i < 5; i++)
            {
                var group = groups[i];
                var card = catalog.FindCard(group.Key);
                var button = CreateButton($"{card.displayName}\n三合一", uiRoot.transform, new Vector2(0.5f, 0.35f), new Vector2(190f, 80f), new Vector2(-390f + i * 195f, 0f));
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

        private void BuildArtifactPanel()
        {
            var info = CreateText("神器说明", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(1000f, 52f), 22, TextAnchor.MiddleCenter);
            info.text = "选择一个神器加入本局，神器会改变战斗、经济或成长。";

            var choices = flow.GenerateArtifactChoices();
            var startX = -((choices.Count - 1) * 260f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var artifact = choices[i];
                var button = CreateButton($"{artifact.displayName}\n{artifact.description}", uiRoot.transform, new Vector2(0.5f, 0.50f), new Vector2(245f, 130f), new Vector2(startX + i * 260f, 0f));
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.rectTransform.sizeDelta = new Vector2(210f, 72f);
                    label.rectTransform.anchoredPosition = new Vector2(0f, -26f);
                    label.fontSize = 18;
                }

                AddSpriteIcon(button.transform, artifact.icon, new Vector2(0f, 28f), new Vector2(50f, 50f));
                button.onClick.AddListener(() =>
                {
                    flow.ChooseArtifact(artifact);
                    BuildUi();
                });
            }
        }

        private void BuildHeader()
        {
            var run = flow.CurrentRun;
            var panel = CreatePanel("顶部信息栏", uiRoot.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1320f, 88f), new Color(0.025f, 0.035f, 0.045f, 0.72f));
            var text = CreateText("顶部信息", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(1280f, 80f), 20, TextAnchor.MiddleCenter);
            text.text = $"迷宫 {run.floor}/3 层  房间进度 {run.row}/10    金币 {run.gold}    生命 {run.playerHp:0}/{flow.PlayerMaxHpForRun():0}    经验 {run.heroExperience}    卡组 {run.deckCardIds.Count}    神器 {run.artifactIds.Count}\n{run.lastMessage}";
        }

        private void BuildBackdrop(Transform root)
        {
            var image = CreatePanel("背景", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.055f, 0.052f, 1f));
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;

            var texture = Resources.Load<Texture2D>("UI/battlefield_honghuang_ai");
            if (texture != null)
            {
                image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                image.preserveAspect = false;
                image.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            }
        }

        private Button CreateNodeButton(MapNodeRuntime node, Transform parent, Vector2 anchor, Vector2 offset)
        {
            var button = CreateButton(GameFlowController.NodeTypeName(node.NodeType), parent, anchor, new Vector2(196f, 128f), offset);
            var image = button.GetComponent<Image>();
            image.color = node.NodeType switch
            {
                MapNodeType.NormalMonster => new Color(0.19f, 0.24f, 0.22f, 0.96f),
                MapNodeType.EliteMonster => new Color(0.36f, 0.17f, 0.13f, 0.96f),
                MapNodeType.Shop => new Color(0.28f, 0.21f, 0.08f, 0.96f),
                MapNodeType.Rest => new Color(0.12f, 0.28f, 0.22f, 0.96f),
                MapNodeType.Opportunity => new Color(0.22f, 0.20f, 0.36f, 0.96f),
                MapNodeType.Mystery => new Color(0.16f, 0.13f, 0.22f, 0.96f),
                MapNodeType.Artifact => new Color(0.31f, 0.20f, 0.08f, 0.96f),
                MapNodeType.SmallBoss or MapNodeType.FinalBoss => new Color(0.42f, 0.08f, 0.08f, 0.96f),
                _ => image.color
            };
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

        private static void AddSpriteIcon(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            if (sprite == null)
            {
                return;
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
        }

        private static Sprite LoadNodeSprite(MapNodeType nodeType)
        {
            var path = $"UI/Nodes/{NodeIconName(nodeType)}";
            return LoadResourceSprite(path);
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
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
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
