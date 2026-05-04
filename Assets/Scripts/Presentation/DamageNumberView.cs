using System;
using UnityEngine;

namespace XTD.Presentation
{
    public sealed class DamageNumberView : MonoBehaviour
    {
        private const float Duration = 0.55f;

        private Action onComplete;
        private TextMesh text;
        private float age;

        public void Initialize(Vector3 position, int value, Action onComplete)
        {
            this.onComplete = onComplete;
            age = 0f;
            transform.position = position + new Vector3(0f, 0.35f, 0f);
            transform.localScale = Vector3.one;

            if (text == null)
            {
                text = gameObject.GetComponent<TextMesh>();
                if (text == null)
                {
                    text = gameObject.AddComponent<TextMesh>();
                }

                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 40;
                text.characterSize = 0.08f;
                var renderer = text.GetComponent<MeshRenderer>();
                renderer.sortingOrder = 50;
            }

            text.text = value.ToString();
            text.color = Color.white;
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += new Vector3(0f, Time.deltaTime * 0.8f, 0f);

            var color = text.color;
            color.a = 1f - Mathf.Clamp01(age / Duration);
            text.color = color;

            if (age >= Duration)
            {
                Complete();
            }
        }

        private void Complete()
        {
            var callback = onComplete;
            onComplete = null;
            callback?.Invoke();
        }
    }
}
