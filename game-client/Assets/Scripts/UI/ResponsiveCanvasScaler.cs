using UnityEngine;
using UnityEngine.UI;

namespace QuizBattle.UI
{
    /// Automatically adapts the CanvasScaler's matchWidthOrHeight dynamically
    /// based on the current window/screen aspect ratio.
    /// In wide landscape (>= 16:9), it matches height (1.0f) so the HUD occupies a fixed
    /// fraction of the vertical screen, leaving the arena visible below.
    /// In narrower aspect ratios (iPad 4:3, laptop 16:10, or portrait mobile),
    /// it shifts toward matching width (0.0f) so UI cards, placards, and buttons
    /// are NEVER cut off horizontally outside the screen boundaries.
    [RequireComponent(typeof(CanvasScaler))]
    public class ResponsiveCanvasScaler : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            UpdateScaleMode();
        }

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                UpdateScaleMode();
            }
        }

        public void UpdateScaleMode()
        {
            if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
            if (_scaler == null) return;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            // 16:9 is ~1.777f.
            // If aspect >= 1.70f: wide landscape, match height (1.0f).
            // If aspect <= 1.0f: portrait or square, match width (0.0f) so 1280 width is preserved 100%.
            // Between 1.0f and 1.70f: smoothly interpolate so neither dimension is clipped.
            float match = Mathf.Clamp01(Mathf.InverseLerp(1.0f, 1.70f, aspect));
            _scaler.matchWidthOrHeight = match;
        }
    }
}
