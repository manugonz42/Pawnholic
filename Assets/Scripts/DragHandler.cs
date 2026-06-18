using UnityEngine;
using UnityEngine.EventSystems;

namespace PawnShop
{
    /// <summary>
    /// Componente genérico para arrastrar un elemento de UI. Reenvía los eventos de arrastre
    /// del EventSystem a unas callbacks que asigna quien lo crea (aquí, PawnShopGame).
    /// </summary>
    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public System.Action<PointerEventData> onBegin;
        public System.Action<PointerEventData> onDragging;
        public System.Action<PointerEventData> onEnd;

        public void OnBeginDrag(PointerEventData e) { onBegin?.Invoke(e); }
        public void OnDrag(PointerEventData e) { onDragging?.Invoke(e); }
        public void OnEndDrag(PointerEventData e) { onEnd?.Invoke(e); }
    }
}
