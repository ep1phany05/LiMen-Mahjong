using UnityEngine;

namespace LiMen.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;

        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeAreaIfChanged(true);
        }

        void OnEnable()
        {
            ApplySafeAreaIfChanged(true);
        }

        void Update()
        {
            ApplySafeAreaIfChanged(false);
        }

        private void ApplySafeAreaIfChanged(bool force)
        {
            Rect safeArea = Screen.safeArea;
            if (!force && safeArea == _lastSafeArea)
                return;

            _lastSafeArea = safeArea;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}
