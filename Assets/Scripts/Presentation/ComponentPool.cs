using System;
using System.Collections.Generic;
using UnityEngine;

namespace XTD.Presentation
{
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly Func<T> factory;
        private readonly Queue<T> inactive = new();

        public ComponentPool(Func<T> factory)
        {
            this.factory = factory;
        }

        public T Get()
        {
            T item = null;
            while (inactive.Count > 0 && item == null)
            {
                item = inactive.Dequeue();
            }

            if (item == null)
            {
                item = factory != null ? factory() : null;
            }

            if (item == null)
            {
                Debug.LogWarning($"神魔镇荒：对象池 {typeof(T).Name} 创建实例失败。");
                return null;
            }

            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }

            item.gameObject.SetActive(false);
            inactive.Enqueue(item);
        }
    }
}
