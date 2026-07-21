using UnityEngine;
using UnityEngine.EventSystems;

namespace Splice.Input
{
    // Lightweight UI joystick for portrait mobile. Presentation-only; HeroInputController turns Value into
    // a camera-relative world direction and sends that intent to the server.
    public class HeroVirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [Min(10f)] [SerializeField] private float radius = 80f;

        public Vector2 Value { get; private set; }

        private void Awake()
        {
            if (background == null) background = transform as RectTransform;
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateValue(eventData);
        public void OnDrag(PointerEventData eventData) => UpdateValue(eventData);

        public void OnPointerUp(PointerEventData eventData)
        {
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }

        private void UpdateValue(PointerEventData eventData)
        {
            if (background == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out var local))
                return;

            Value = Vector2.ClampMagnitude(local / Mathf.Max(1f, radius), 1f);
            if (handle != null) handle.anchoredPosition = Value * radius;
        }
    }
}
