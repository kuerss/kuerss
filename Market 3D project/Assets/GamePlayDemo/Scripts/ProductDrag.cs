using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamePlayDemo
{
    public class ProductDrag : MonoBehaviour, IDragHandler
    {
        public Action<float> onDrag;

        public void OnDrag(PointerEventData eventData)
        {
            if (onDrag != null) onDrag(-eventData.delta.x * 0.55f);
        }
    }
}
