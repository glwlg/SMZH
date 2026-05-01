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
            transform.position = position;
            transform.localScale = Vector3.one * baseScale;
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite != null ? sprite : RuntimeSpriteFactory.EffectSprite;
            spriteRenderer.color = faction == Faction.Player ? Color.white : new Color(1f, 0.58f, 0.48f, 0.95f);
            spriteRenderer.sortingOrder = 25;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / Duration);
            transform.localScale = Vector3.one * Mathf.Lerp(baseScale, baseScale * 2.8f, t);
            var color = spriteRenderer.color;
            color.a = 1f - t;
            spriteRenderer.color = color;
            if (age >= Duration)
            {
                onComplete?.Invoke();
            }
        }
    }
}
