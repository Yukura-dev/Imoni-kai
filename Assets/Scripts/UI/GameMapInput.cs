using UnityEngine;
using UnityEngine.EventSystems;

namespace Imoni.UI
{
    /// <summary>Converts a map press into one fixed destination in world space.</summary>
    public sealed class GameMapInput : MonoBehaviour, IPointerDownHandler
    {
        public GameScreenController controller;
        public RectTransform world;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || controller == null || world == null || !controller.CanMoveOnMap)
                return;

            // Touch pointers use Left; a desktop player may also right-click to move.
            if (eventData.button != PointerEventData.InputButton.Left &&
                eventData.button != PointerEventData.InputButton.Right)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    world, eventData.position, eventData.pressEventCamera, out Vector2 point))
            {
                controller.MoveToWorldPoint(point);
            }
        }
    }
}
