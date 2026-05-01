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
            var item = inactive.Count > 0 ? inactive.Dequeue() : factory();
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
