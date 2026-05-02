using System;
using UnityEngine;
using XTD.Content;

namespace XTD.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SimpleEffectView : MonoBehaviour
    {
        private Action onComplete;
        private SpriteRenderer spriteRenderer;
        private float age;
        private float baseScale;
        private float endScale;
        private float duration = Duration;
        private Color startColor;
        private const float Duration = 0.22f;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Vector3 position, Faction faction, Sprite sprite, float scale, Action onComplete)
        {
            this.onComplete = onComplete;
            age = 0f;
            baseScale = scale;
            endScale = baseScale * 2.8f;
            duration = Duration;
            transform.position = position;
            transform.localScale = Vector3.one * baseScale;
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite != null ? sprite : RuntimeSpriteFactory.EffectSprite;
            startColor = faction == Faction.Player ? Color.white : new Color(1f, 0.58f, 0.48f, 0.95f);
            spriteRenderer.color = startColor;
            spriteRenderer.sortingOrder = 25;
        }

        public void InitializeCustom(Vector3 position, Color color, Sprite sprite, float startScale, float targetScale, float effectDuration, int sortingOrder, Action onComplete)
        {
            this.onComplete = onComplete;
            age = 0f;
            baseScale = startScale;
            endScale = targetScale;
            duration = Mathf.Max(0.05f, effectDuration);
            startColor = color;
            transform.position = position;
            transform.localScale = Vector3.one * baseScale;
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite != null ? sprite : RuntimeSpriteFactory.EffectSprite;
            spriteRenderer.color = startColor;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(baseScale, endScale, t);
            var color = startColor;
            color.a = startColor.a * (1f - t);
            spriteRenderer.color = color;
            if (age >= duration)
            {
                onComplete?.Invoke();
            }
        }
    }
}
