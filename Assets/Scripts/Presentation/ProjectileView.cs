using System;
using UnityEngine;
using XTD.Content;

namespace XTD.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ProjectileView : MonoBehaviour
    {
        private const float Speed = 12f;
        private Action onComplete;
        private Vector3 target;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Vector3 start, Vector3 end, Faction faction, Sprite sprite, Action onComplete)
        {
            this.onComplete = onComplete;
            target = end;
            transform.position = start;
            transform.localScale = Vector3.one * 0.35f;
            var direction = end - start;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            }

            spriteRenderer ??= GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite != null ? sprite : RuntimeSpriteFactory.ProjectileSprite;
            spriteRenderer.color = faction == Faction.Player ? Color.white : new Color(1f, 0.58f, 0.48f);
            spriteRenderer.sortingOrder = 24;
        }

        private void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, target, Speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, target) <= 0.08f)
            {
                onComplete?.Invoke();
            }
        }
    }
}
