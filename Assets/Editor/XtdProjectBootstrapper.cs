using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using XTD.Battle;
using XTD.Content;
using XTD.Flow;
using XTD.Presentation;

namespace XTD.Editor
{
    public static class XtdProjectBootstrapper
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string SceneRoot = ProjectRoot + "/Scenes";
        private const string ContentRoot = ProjectRoot + "/Content";
        private const string ArtRoot = ProjectRoot + "/Art";
        private const string AiArtRoot = ArtRoot + "/AI";
        private const string AiBattleRoot = AiArtRoot + "/Battle";
        private const string AiCardsRoot = AiArtRoot + "/Cards";
        private const string AiFxRoot = AiArtRoot + "/FX";
        private const string AiBackgroundRoot = AiArtRoot + "/Backgrounds";
        private const string AiSourceRoot = AiArtRoot + "/SourceSheets";
        private const string AiUiRoot = AiArtRoot + "/UI";
        private const string AiNodeIconRoot = AiUiRoot + "/Nodes";
        private const string AiArtifactIconRoot = AiUiRoot + "/Artifacts";
        private const string SettingsRoot = ProjectRoot + "/Settings";
        private const string ResourcesRoot = "Assets/Resources";
        private const string ResourcesAiArtRoot = ResourcesRoot + "/Art/AI";
        private const string BattleMusicAssetPath = "Assets/Resources/Audio/BGM/hyoshi_action_track_2.ogg";
        private const string CatalogPath = ContentRoot + "/GameContentCatalog.asset";

        [MenuItem("神魔镇荒/初始化/重建 MVP 原型内容")]
        [MenuItem("神魔镇荒/Bootstrap/Create MVP Project Content")]
        public static void CreateMvpProjectContent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("神魔镇荒", "正在播放时不能重建场景。请先停止 Play，再执行初始化菜单。", "知道了");
                }

                return;
            }

            EnsureFolders();
            PrepareAiSprites();
            var catalog = CreateCatalogAsset();
            CreateBootScene();
            CreateMainMenuScene(catalog);
            CreateBattlePrototypeScene(catalog);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("神魔镇荒", "MVP 原型内容和场景已重建。请打开 BattlePrototype 场景并点击 Play。", "知道了");
            }
        }

        [MenuItem("神魔镇荒/验证/MVP 内容校验")]
        public static void ValidateMvpContent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = GameContentFactory.CreateCatalog();
            }

            var report = MvpValidationService.Validate(catalog);
            var message = report.Passed
                ? "MVP 校验通过：卡牌、神器、敌人、迷宫结构和最终首领闭环均满足当前范围。"
                : report.ToString();

            if (report.Passed)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("神魔镇荒 MVP 校验", message, "知道了");
            }
        }

        [MenuItem("神魔镇荒/打包/同步 Resources 资源")]
        public static void PrepareBuildResources()
        {
            EnsureFolders();
            PrepareAiSprites();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(ProjectRoot);
            Directory.CreateDirectory(SceneRoot);
            Directory.CreateDirectory(ContentRoot);
            Directory.CreateDirectory(ArtRoot);
            Directory.CreateDirectory(AiArtRoot);
            Directory.CreateDirectory(SettingsRoot);
            Directory.CreateDirectory(AiBattleRoot);
            Directory.CreateDirectory(AiCardsRoot);
            Directory.CreateDirectory(AiFxRoot);
            Directory.CreateDirectory(AiBackgroundRoot);
            Directory.CreateDirectory(AiSourceRoot);
            Directory.CreateDirectory(AiUiRoot);
            Directory.CreateDirectory(AiNodeIconRoot);
            Directory.CreateDirectory(AiArtifactIconRoot);
            Directory.CreateDirectory(ResourcesAiArtRoot);
            Directory.CreateDirectory(ResourcesAiArtRoot + "/Backgrounds");
            Directory.CreateDirectory(ResourcesAiArtRoot + "/Battle");
            Directory.CreateDirectory(ResourcesAiArtRoot + "/Cards");
            Directory.CreateDirectory(ResourcesAiArtRoot + "/FX");
            Directory.CreateDirectory(ResourcesAiArtRoot + "/UI");
        }

        private static ContentCatalog CreateCatalogAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(CatalogPath);
            }

            var catalog = GameContentFactory.CreateCatalog();
            AssignPrototypeArt(catalog);
            AssetDatabase.CreateAsset(catalog, CatalogPath);

            foreach (var unit in catalog.units)
            {
                AssetDatabase.AddObjectToAsset(unit, catalog);
            }

            foreach (var card in catalog.cards)
            {
                AssetDatabase.AddObjectToAsset(card, catalog);
            }

            foreach (var artifact in catalog.artifacts)
            {
                AssetDatabase.AddObjectToAsset(artifact, catalog);
            }

            foreach (var encounter in catalog.encounters)
            {
                AssetDatabase.AddObjectToAsset(encounter, catalog);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceUpdate);

            var savedCatalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            return savedCatalog != null ? savedCatalog : catalog;
        }

        private static void AssignPrototypeArt(ContentCatalog catalog)
        {
            SetUnitArt(catalog, "unit_militia", LoadAiBattleSprite("unit_militia_battle"));
            SetUnitArt(catalog, "unit_archer", LoadAiBattleSprite("unit_archer_battle"));
            SetUnitArt(catalog, "unit_shield_guard", LoadAiBattleSprite("unit_heaven_general_battle"));
            SetUnitArt(catalog, "unit_monkey_vanguard", LoadAiBattleSprite("unit_monkey_vanguard_battle"));
            SetUnitArt(catalog, "unit_thunder_guard", LoadAiBattleSprite("unit_thunder_guard_battle"));
            SetUnitArt(catalog, "unit_incense_barracks", LoadAiBattleSprite("unit_incense_barracks_battle"));
            SetUnitArt(catalog, "unit_spirit_arrow_altar", LoadAiBattleSprite("unit_spirit_arrow_altar_battle"));
            SetUnitArt(catalog, "unit_roadblock", LoadAiBattleSprite("unit_bagua_wall_battle"));
            SetUnitArt(catalog, "unit_thunder_drum_tower", LoadAiBattleSprite("unit_thunder_drum_tower_battle"));
            SetUnitArt(catalog, "enemy_grunt", LoadAiBattleSprite("enemy_grunt_battle"));
            SetUnitArt(catalog, "enemy_brute", LoadAiBattleSprite("enemy_brute_battle"));
            SetUnitArt(catalog, "enemy_alpha", LoadAiBattleSprite("enemy_wolf_elite_battle"));
            SetUnitArt(catalog, "enemy_imp_archer", LoadAiBattleSprite("enemy_imp_archer_battle"));
            SetUnitArt(catalog, "enemy_venom_shaman", LoadAiBattleSprite("enemy_venom_shaman_battle"));
            SetUnitArt(catalog, "enemy_wolf_elite", LoadAiBattleSprite("enemy_wolf_elite_battle"));
            SetUnitArt(catalog, "enemy_bone_elite", LoadAiBattleSprite("enemy_bone_elite_battle"));
            SetUnitArt(catalog, "enemy_ox_elite", LoadAiBattleSprite("enemy_ox_elite_battle"));
            SetUnitArt(catalog, "boss_black_wind", LoadAiBattleSprite("boss_black_wind_battle"));
            SetUnitArt(catalog, "boss_bone_queen", LoadAiBattleSprite("boss_bone_queen_battle"));
            SetUnitArt(catalog, "boss_chaos_lord", LoadAiBattleSprite("boss_chaos_lord_battle"));

            SetLeveledCardArt(catalog, "card_incense_barracks", LoadAiCardSprite("card_incense_barracks"));
            SetLeveledCardArt(catalog, "card_spirit_arrow_altar", LoadAiCardSprite("card_spirit_arrow_altar"));
            SetLeveledCardArt(catalog, "card_roadblock", LoadAiCardSprite("card_roadblock"));
            SetLeveledCardArt(catalog, "card_heaven_soldier_talisman", LoadAiCardSprite("card_heaven_soldier_talisman"));
            SetLeveledCardArt(catalog, "card_heaven_general_order", LoadAiCardSprite("card_heaven_general_order"));
            SetLeveledCardArt(catalog, "card_fireball", LoadAiCardSprite("card_fireball"));
            SetLeveledCardArt(catalog, "card_rally", LoadAiCardSprite("card_rally"));
            SetLeveledCardArt(catalog, "card_thunder_drum_tower", LoadAiCardSprite("card_thunder_drum_tower"));
            SetLeveledCardArt(catalog, "card_monkey_hero", LoadAiCardSprite("card_monkey_hero"));
            SetLeveledCardArt(catalog, "card_thunder_talisman", LoadAiCardSprite("card_thunder_talisman"));
            SetLeveledCardArt(catalog, "card_golden_barrier", LoadAiCardSprite("card_golden_barrier"));

            SetArtifactIcon(catalog, "artifact_long_banner", LoadAiArtifactSprite("artifact_long_banner"));
            SetArtifactIcon(catalog, "artifact_field_purse", LoadAiArtifactSprite("artifact_field_purse"));
            SetArtifactIcon(catalog, "artifact_war_drum", LoadAiArtifactSprite("artifact_war_drum"));
            SetArtifactIcon(catalog, "artifact_heaven_seal", LoadAiArtifactSprite("artifact_heaven_seal"));
            SetArtifactIcon(catalog, "artifact_jade_bottle", LoadAiArtifactSprite("artifact_jade_bottle"));
            SetArtifactIcon(catalog, "artifact_fire_pearl", LoadAiArtifactSprite("artifact_fire_pearl"));
            SetArtifactIcon(catalog, "artifact_cloud_boots", LoadAiArtifactSprite("artifact_cloud_boots"));
            SetArtifactIcon(catalog, "artifact_black_tortoise", LoadAiArtifactSprite("artifact_black_tortoise"));
            SetArtifactIcon(catalog, "artifact_market_token", LoadAiArtifactSprite("artifact_market_token"));
            SetArtifactIcon(catalog, "artifact_artifact_eye", LoadAiArtifactSprite("artifact_artifact_eye"));
            SetArtifactIcon(catalog, "artifact_dragon_bone", LoadAiArtifactSprite("artifact_dragon_bone"));
            SetArtifactIcon(catalog, "artifact_command_seal", LoadAiArtifactSprite("artifact_command_seal"));
            SetArtifactIcon(catalog, "artifact_fox_coin", LoadAiArtifactSprite("artifact_fox_coin"));
            SetArtifactIcon(catalog, "artifact_taiji_map", LoadAiArtifactSprite("artifact_taiji_map"));
            SetArtifactIcon(catalog, "artifact_star_sand", LoadAiArtifactSprite("artifact_star_sand"));
            SetArtifactIcon(catalog, "artifact_battle_scripture", LoadAiArtifactSprite("artifact_battle_scripture"));
            SetArtifactIcon(catalog, "artifact_vajra", LoadAiArtifactSprite("artifact_vajra"));
            SetArtifactIcon(catalog, "artifact_permanent_relic", LoadAiArtifactSprite("artifact_permanent_relic"));
        }

        private static void PrepareAiSprites()
        {
            AssetDatabase.Refresh();
            if (!Directory.Exists(AiArtRoot))
            {
                return;
            }

            SyncAiSpritesToResources();
            AssetDatabase.Refresh();

            foreach (var path in Directory.GetFiles(AiArtRoot, "*.png", SearchOption.AllDirectories))
            {
                var unityPath = path.Replace("\\", "/");
                PrepareSpriteImport(unityPath);
            }

            if (Directory.Exists(ResourcesAiArtRoot))
            {
                foreach (var path in Directory.GetFiles(ResourcesAiArtRoot, "*.png", SearchOption.AllDirectories))
                {
                    var unityPath = path.Replace("\\", "/");
                    PrepareSpriteImport(unityPath);
                }
            }
        }

        private static void SyncAiSpritesToResources()
        {
            CopyPngFolder(AiBackgroundRoot, ResourcesAiArtRoot + "/Backgrounds");
            CopyPngFolder(AiBattleRoot, ResourcesAiArtRoot + "/Battle");
            CopyPngFolder(AiCardsRoot, ResourcesAiArtRoot + "/Cards");
            CopyPngFolder(AiFxRoot, ResourcesAiArtRoot + "/FX");
        }

        private static void CopyPngFolder(string sourceRoot, string targetRoot)
        {
            if (!Directory.Exists(sourceRoot))
            {
                return;
            }

            Directory.CreateDirectory(targetRoot);
            foreach (var sourcePath in Directory.GetFiles(sourceRoot, "*.png", SearchOption.TopDirectoryOnly))
            {
                var targetPath = Path.Combine(targetRoot, Path.GetFileName(sourcePath)).Replace("\\", "/");
                File.Copy(sourcePath, targetPath, true);
            }
        }

        private static void PrepareSpriteImport(string unityPath)
        {
            if (AssetImporter.GetAtPath(unityPath) is not TextureImporter importer)
            {
                return;
            }

            var isBackground = unityPath.Contains("/Backgrounds/", StringComparison.Ordinal);
            var isBattle = unityPath.Contains("/Battle/", StringComparison.Ordinal);
            var isCard = unityPath.Contains("/Cards/", StringComparison.Ordinal);
            var pixelsPerUnit = isBackground ? 128f : 256f;
            var filterMode = FilterMode.Bilinear;
            var maxTextureSize = isBackground ? 2048 : isBattle ? 2048 : isCard ? 1024 : 512;
            var compression = isBackground ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
            var mipmaps = isBackground;

            var changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
            {
                importer.spritePixelsPerUnit = pixelsPerUnit;
                changed = true;
            }

            if (importer.filterMode != filterMode)
            {
                importer.filterMode = filterMode;
                changed = true;
            }

            if (importer.maxTextureSize != maxTextureSize)
            {
                importer.maxTextureSize = maxTextureSize;
                changed = true;
            }

            if (importer.textureCompression != compression)
            {
                importer.textureCompression = compression;
                changed = true;
            }

            if (importer.compressionQuality != 65)
            {
                importer.compressionQuality = 65;
                changed = true;
            }

            if (importer.alphaIsTransparency != true)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }

            if (importer.mipmapEnabled != mipmaps)
            {
                importer.mipmapEnabled = mipmaps;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void SetUnitArt(ContentCatalog catalog, string unitId, Sprite sprite)
        {
            var unit = catalog.FindUnit(unitId);
            if (unit == null)
            {
                return;
            }

            unit.art = sprite;
            unit.tint = Color.white;
            if (sprite == null)
            {
                Debug.LogWarning($"未找到单位素材：{unitId}");
            }
        }

        private static void SetCardArt(ContentCatalog catalog, string cardId, Sprite sprite)
        {
            var card = catalog.FindCard(cardId);
            if (card == null)
            {
                return;
            }

            card.art = sprite;
            if (sprite == null)
            {
                Debug.LogWarning($"未找到卡牌素材：{cardId}");
            }
        }

        private static void SetLeveledCardArt(ContentCatalog catalog, string baseCardId, Sprite sprite)
        {
            SetCardArt(catalog, baseCardId, sprite);
            SetCardArt(catalog, baseCardId + "_lv2", sprite);
            SetCardArt(catalog, baseCardId + "_lv3", sprite);
        }

        private static void SetArtifactIcon(ContentCatalog catalog, string artifactId, Sprite sprite)
        {
            var artifact = catalog.FindArtifact(artifactId);
            if (artifact == null)
            {
                return;
            }

            artifact.icon = sprite;
            if (sprite == null)
            {
                Debug.LogWarning($"未找到神器图标：{artifactId}");
            }
        }

        private static Sprite LoadAiBattleSprite(string spriteName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{AiBattleRoot}/{spriteName}.png");
        }

        private static Sprite LoadAiCardSprite(string spriteName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{AiCardsRoot}/{spriteName}.png");
        }

        private static Sprite LoadAiFxSprite(string spriteName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{AiFxRoot}/{spriteName}.png");
        }

        private static Sprite LoadAiBackgroundSprite(string spriteName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{AiBackgroundRoot}/{spriteName}.png");
        }

        private static Sprite LoadAiArtifactSprite(string spriteName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{AiArtifactIconRoot}/{spriteName}.png");
        }

        private static AudioClip LoadBattleMusicClip()
        {
            AssetDatabase.ImportAsset(BattleMusicAssetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(BattleMusicAssetPath);
        }

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("流程控制器").AddComponent<GameFlowController>();
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, SceneRoot + "/Boot.unity");
        }

        private static void CreateMainMenuScene(ContentCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("主菜单控制器").AddComponent<MainMenuController>();
            var controller = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            if (controller != null)
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("catalog").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, SceneRoot + "/MainMenu.unity");
        }

        private static void CreateBattlePrototypeScene(ContentCatalog catalog)
        {
            var savedCatalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            if (savedCatalog != null)
            {
                catalog = savedCatalog;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateCamera();

            var controller = new GameObject("战斗控制器").AddComponent<BattleController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("defaultCatalog").objectReferenceValue = catalog;
            SetSerializedObject(serialized, "playerProjectileSprite", LoadAiFxSprite("projectile_spirit_arrow"));
            SetSerializedObject(serialized, "enemyProjectileSprite", LoadAiFxSprite("projectile_spirit_arrow"));
            SetSerializedObject(serialized, "hitEffectSprite", LoadAiFxSprite("fx_hit_jade_spark"));
            SetSerializedObject(serialized, "spellImpactSprite", LoadAiFxSprite("fx_samadhi_fire_impact"));
            SetSerializedObject(serialized, "battleMusicClip", LoadBattleMusicClip());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var stage = new GameObject("2D 战场背景").AddComponent<BattleStage2D>();
            var stageSerialized = new SerializedObject(stage);
            SetSerializedObject(stageSerialized, "backdropSprite", LoadAiBackgroundSprite("battlefield_honghuang_ai"));
            SetSerializedObject(stageSerialized, "floorSprite", null);
            SetSerializedObject(stageSerialized, "playerPortalSprite", null);
            SetSerializedObject(stageSerialized, "enemyGateSprite", null);
            stageSerialized.ApplyModifiedPropertiesWithoutUndo();
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, SceneRoot + "/BattlePrototype.unity");
        }

        private static void SetSerializedObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.075f);
            return camera;
        }

        private static void CreateSceneSprite(string name, Sprite sprite, Vector3 position, Vector3 scale, Color color, int sortingOrder)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.position = position;
            spriteObject.transform.localScale = scale;
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneRoot + "/Boot.unity", true),
                new EditorBuildSettingsScene(SceneRoot + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(SceneRoot + "/BattlePrototype.unity", true)
            };
        }

        private static void CreateEventSystem()
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

            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }
    }

    public sealed class XtdBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            XtdProjectBootstrapper.PrepareBuildResources();
        }
    }
}
