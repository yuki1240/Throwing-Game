using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BallGame
{
    /// <summary>
    /// 画像のクリック・タップを受け取り、画像の中の位置（0〜1、左下が (0,0)）を知らせます。
    /// UI のボタンの上を押したときは届きません。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PointerReceiver : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>押されたとき。引数は画像の中の 0〜1（左下が (0,0)）。</summary>
        public event Action<Vector2> Pressed;

        /// <summary>
        /// 押されたときに呼ばれます。
        /// </summary>
        /// <param name="eventData">押した情報</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransform rect = (RectTransform)transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out local))
            {
                return;
            }

            Pressed?.Invoke(Rect.PointToNormalized(rect.rect, local));
        }
    }
}
