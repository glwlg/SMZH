using UnityEngine;
using XTD.Content;

namespace XTD.Presentation
{
    public sealed class BattleBaseView : MonoBehaviour
    {
        private SpriteRenderer auraRenderer;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer healthBack;
        private SpriteRenderer healthFill;
        private Faction faction;
        private Color bodyColor;
        private float baseScale;
        private float maxHp;
        private float flash;
        private float seed;

        private void Awake()
        {
            BuildParts();
        }

        public void Initialize(Faction side, Sprite sprite, Vector3 position, float scale, float maximumHp)
        {
            faction = side;
            baseScale = scale;
            maxHp = Mathf.Max(1f, maximumHp);
            seed = Random.Range(0f, 50f);
            transform.position = position;
            transform.localScale = Vector3.one * baseScale;
            gameObject.name = side == Faction.Player ? "我方大本营" : "敌方大本营";

            BuildParts();
            bodyColor = side == Faction.Player ? Color.white : new Color(1f, 0.48f, 0.42f, 1f);
            bodyRenderer.sprite = sprite != null ? sprite : RuntimeSpriteFactory.UnitSprite;
            bodyRenderer.color = bodyColor;
            bodyRenderer.sortingOrder = 8;

            auraRenderer.sprite = RuntimeSpriteFactory.EffectSprite;
            auraRenderer.color = side == Faction.Player
                ? new Color(0.25f, 1f, 0.85f, 0.24f)
                : new Color(1f, 0.18f, 0.12f, 0.28f);
            auraRenderer.sortingOrder = 6;

            healthBack.sortingOrder = 35;
            healthFill.sortingOrder = 36;
            UpdateHealth(maxHp, maxHp);
        }

        public void UpdateHealth(float currentHp, float maximumHp)
        {
            maxHp = Mathf.Max(1f, maximumHp);
            var percent = Mathf.Clamp01(currentHp / maxHp);
            healthBack.transform.localScale = new Vector3(1.25f, 0.10f, 1f);
            healthFill.transform.localPosition = new Vector3(-0.625f + 0.625f * percent, 0f, -0.02f);
            healthFill.transform.localScale = new Vector3(1.25f * percent, 0.07f, 1f);
            healthFill.color = Color.Lerp(new Color(1f, 0.18f, 0.10f), new Color(0.30f, 1f, 0.35f), percent);
        }

        public void Flash()
        {
            flash = 1f;
        }

        private void Update()
        {
            var pulse = Mathf.Sin(Time.time * 2.0f + seed) * 0.035f;
            transform.localScale = Vector3.one * (baseScale + pulse);

            auraRenderer.transform.localScale = Vector3.one * (1.8f + Mathf.Sin(Time.time * 2.8f + seed) * 0.18f);
            auraRenderer.transform.Rotate(0f, 0f, (faction == Faction.Player ? 12f : -16f) * Time.deltaTime);

            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4.5f);
            bodyRenderer.color = Color.Lerp(bodyColor, Color.white, flash);
        }

        private void BuildParts()
        {
            if (bodyRenderer != null)
            {
                return;
            }

            auraRenderer = CreatePart("阵营光环", Vector3.zero);
            auraRenderer.sprite = RuntimeSpriteFactory.EffectSprite;

            bodyRenderer = CreatePart("主体", Vector3.zero);

            var healthRoot = new GameObject("基地血条").transform;
            healthRoot.SetParent(transform, false);
            healthRoot.localPosition = new Vector3(0f, 0.92f, -0.02f);

            healthBack = CreatePart("血条底色", Vector3.zero, healthRoot);
            healthBack.sprite = RuntimeSpriteFactory.UnitSprite;
            healthBack.color = new Color(0.05f, 0.05f, 0.06f, 0.88f);

            healthFill = CreatePart("血量", Vector3.zero, healthRoot);
            healthFill.sprite = RuntimeSpriteFactory.UnitSprite;
        }

        private SpriteRenderer CreatePart(string partName, Vector3 localPosition, Transform parent = null)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = localPosition;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.UnitSprite;
            return renderer;
        }
    }
}
