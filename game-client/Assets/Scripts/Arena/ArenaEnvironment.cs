using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuizBattle.Arena
{
    public class ArenaRig
    {
        public Camera Camera;
        public Light KeyLight;
        public Light FillLight;
    }

    /// Single source of truth for camera + lighting + post-processing, replacing four
    /// separate camera-creation/framing duplicates (GameManager, ArenaController,
    /// NetworkedArenaView, NetworkedMatchDemoRunner). Acquire() reuses an existing
    /// Camera.main/directional Light when the scene already authored one (the real
    /// Arena.unity scene does — this also fixes ArenaController previously creating a
    /// second MainCamera-tagged camera on top of it) and creates them when absent (the
    /// headless demo runners build an empty scene with neither).
    public static class ArenaEnvironment
    {
        public static ArenaRig Acquire(Color backgroundColor)
        {
            var rig = new ArenaRig
            {
                Camera = AcquireCamera(backgroundColor),
                KeyLight = AcquireKeyLight(),
                FillLight = AcquireFillLight(),
            };

            ConfigureAmbient();
            ConfigureVolume(rig.Camera);
            return rig;
        }

        public static void FrameGrid(ArenaRig rig, GridController grid, int width, int height)
        {
            if (rig?.Camera == null) return;
            var framer = rig.Camera.GetComponent<ArenaCameraAutoFramer>();
            if (framer == null)
            {
                framer = rig.Camera.gameObject.AddComponent<ArenaCameraAutoFramer>();
            }
            framer.SetGridTransform(grid != null ? grid.transform : null);
            framer.SetDimensions(width, height, grid != null ? grid.tileSize : 1.32f);
        }

        private static Camera AcquireCamera(Color backgroundColor)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var camObj = new GameObject("Main Camera");
                camera = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            camera.orthographic = false;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 160f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = QuizBattlePalette.SkyHorizon;

            var camData = camera.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;

            return camera;
        }

        private static Light AcquireKeyLight()
        {
            Light key = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.name != "Fill Light") { key = light; break; }
            }

            if (key == null)
            {
                var go = new GameObject("Key Light");
                key = go.AddComponent<Light>();
                key.type = LightType.Directional;
            }

            key.color = new Color(1.00f, 0.88f, 0.72f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.None; // Crisp toon presentation without dynamic shadow blobs on tiles
            key.shadowStrength = 0f;
            key.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

            RenderSettings.sun = key;
            return key;
        }

        private static Light AcquireFillLight()
        {
            Light fill = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.name == "Fill Light") { fill = light; break; }
            }
            if (fill == null) fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.46f, 0.69f, 1.00f);
            fill.intensity = 0.42f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
            return fill;
        }

        private static void ConfigureAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.43f, 0.49f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.37f, 0.49f);
            RenderSettings.ambientGroundColor = new Color(0.19f, 0.27f, 0.31f);
            RenderSettings.ambientIntensity = 1f;
        }

        private static void ConfigureVolume(Camera camera)
        {
            var existing = Object.FindFirstObjectByType<Volume>();
            if (existing != null) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.15f;
            bloom.intensity.value = 0.28f;
            bloom.scatter.value = 0.55f;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = 3;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.value = 8f;
            colorAdjustments.contrast.value = 10f;
            colorAdjustments.postExposure.value = 0.08f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.08f;
            vignette.smoothness.value = 0.6f;

            var volumeObj = new GameObject("Post Volume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.profile = profile;
        }
    }

    /// Dynamically and responsively calculates camera framing and distance whenever the
    /// window or screen size/aspect ratio changes (including live window resize in WebGL and mobile).
    /// Guarantees that the North wall and Win Line are always comfortably visible below the HUD
    /// and that the arena boundaries fit within any aspect ratio (from 4:3 iPad to 21:9 ultra-wide phones).
    public class ArenaCameraAutoFramer : MonoBehaviour
    {
        public int GridWidth = 8;
        public int GridHeight = 10;
        public float TileSize = 1.32f;
        private Camera _cam;
        private int _lastWidth;
        private int _lastHeight;
        private float _lastAspect;
        private Transform _gridTransform;
        private Matrix4x4 _lastGridMatrix;
        private Rect _lastViewport;
        private QuizBattle.UI.HUD.HudController _hud;
        private float _nextHudCheck;
        private readonly Vector3[] _hudCorners = new Vector3[4];

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void SetDimensions(int width, int height, float tileSize = 1.32f)
        {
            GridWidth = Mathf.Max(1, width);
            GridHeight = Mathf.Max(1, height);
            TileSize = Mathf.Max(0.01f, tileSize);
            ApplyFraming();
        }

        public void SetGridTransform(Transform gridTransform)
        {
            _gridTransform = gridTransform;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || Mathf.Abs(_cam.aspect - _lastAspect) > 0.005f
                    || _lastViewport != GetBoardViewport()
                    || _lastGridMatrix != (_gridTransform != null ? _gridTransform.localToWorldMatrix : Matrix4x4.identity))
            {
                ApplyFraming();
            }
        }

        private Rect GetBoardViewport()
        {
            var pixels = _cam.pixelRect;
            var safe = Screen.safeArea;
            float pixelWidth = Mathf.Max(1f, pixels.width);
            float pixelHeight = Mathf.Max(1f, pixels.height);
            float left = Mathf.Max(0.06f, (safe.xMin - pixels.xMin) / pixelWidth + 0.02f);
            float right = Mathf.Min(0.94f, (safe.xMax - pixels.xMin) / pixelWidth - 0.02f);
            float bottom = Mathf.Max(0.08f, (safe.yMin - pixels.yMin) / pixelHeight + 0.02f);
            float top = Mathf.Min(0.60f, (safe.yMax - pixels.yMin) / pixelHeight - 0.02f);

            if (_hud == null && Time.unscaledTime >= _nextHudCheck)
            {
                _hud = Object.FindFirstObjectByType<QuizBattle.UI.HUD.HudController>();
                _nextHudCheck = Time.unscaledTime + 0.5f;
            }
            if (_hud != null && _hud.isActiveAndEnabled)
            {
                var canvas = _hud.GetComponent<Canvas>();
                var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                for (int i = 0; i < _hud.transform.childCount; i++)
                {
                    var child = _hud.transform.GetChild(i) as RectTransform;
                    if (child == null || !child.gameObject.activeInHierarchy) continue;
                    bool lower = child.name == "WaitBanner";
                    if (!lower && child.name != "QuestionPlacard" && child.name != "TimerBanner" && !child.name.StartsWith("Choice_")) continue;
                    child.GetWorldCorners(_hudCorners);
                    float minY = float.PositiveInfinity;
                    float maxY = float.NegativeInfinity;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float y = (RectTransformUtility.WorldToScreenPoint(uiCamera, _hudCorners[corner]).y - pixels.yMin) / pixelHeight;
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                    if (lower) bottom = Mathf.Max(bottom, maxY + 0.02f);
                    else top = Mathf.Min(top, minY - 0.02f);
                }
            }
            left = Mathf.Clamp(left, 0.02f, 0.80f);
            right = Mathf.Clamp(right, left + 0.10f, 0.98f);
            bottom = Mathf.Clamp(bottom, 0.02f, 0.80f);
            top = Mathf.Clamp(top, bottom + 0.10f, 0.98f);
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        public void ApplyFraming()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;

            _cam.fieldOfView = 32f;
            _cam.nearClipPlane = 0.3f;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastAspect = _cam.aspect;
            _lastViewport = GetBoardViewport();
            _lastGridMatrix = _gridTransform != null ? _gridTransform.localToWorldMatrix : Matrix4x4.identity;
            float centerX = (GridWidth - 1) * TileSize * 0.5f;
            float centerZ = (GridHeight - 1) * TileSize * 0.5f;
            float halfW = GridWidth * TileSize * 0.5f;
            float halfH = GridHeight * TileSize * 0.5f;
            var localCenter = new Vector3(centerX, 0f, centerZ);
            var center = _lastGridMatrix.MultiplyPoint3x4(localCenter);
            var rotation = (_gridTransform != null ? _gridTransform.rotation : Quaternion.identity) * Quaternion.Euler(50f, 0f, 0f);
            var inverseRotation = Quaternion.Inverse(rotation);
            float tangent = Mathf.Tan(_cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float aspect = Mathf.Max(0.01f, _cam.aspect);
            float left = _lastViewport.xMin * 2f - 1f;
            float right = _lastViewport.xMax * 2f - 1f;
            float bottom = _lastViewport.yMin * 2f - 1f;
            float top = _lastViewport.yMax * 2f - 1f;
            float offsetX = (left + right) * 0.5f;
            float offsetY = (bottom + top) * 0.5f;
            float distance = 1f;
            float depth = 0f;
            Bounds[] bounds =
            {
                new Bounds(Vector3.up * 0.6f, new Vector3(halfW * 2f + 1.2f, 4.8f, halfH * 2f + 1.2f)),
                new Bounds(new Vector3(0f, -1.25f, 0.85f), new Vector3(halfW * 2f + 7.4f, 2.5f, halfH * 2f + 6.1f)),
                new Bounds(new Vector3(0f, 2.85f, halfH + 2.9f), new Vector3(halfW * 2f + 7.4f, 5.7f, 4f)),
            };
            foreach (var bound in bounds)
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = localCenter + bound.center + Vector3.Scale(bound.extents, new Vector3(
                                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    var point = inverseRotation * (_lastGridMatrix.MultiplyPoint3x4(local) - center);
                    distance = Mathf.Max(distance, (point.x - right * tangent * aspect * point.z) / ((right - offsetX) * tangent * aspect));
                    distance = Mathf.Max(distance, (left * tangent * aspect * point.z - point.x) / ((offsetX - left) * tangent * aspect));
                    distance = Mathf.Max(distance, (point.y - top * tangent * point.z) / ((top - offsetY) * tangent));
                    distance = Mathf.Max(distance, (bottom * tangent * point.z - point.y) / ((offsetY - bottom) * tangent));
                    distance = Mathf.Max(distance, 1f - point.z);
                    depth = Mathf.Max(depth, point.z);
                }
            }
            var position = center + rotation * new Vector3(-offsetX * distance * tangent * aspect, -offsetY * distance * tangent, -distance);
            transform.SetPositionAndRotation(position, rotation);
            _cam.farClipPlane = Mathf.Max(200f, distance + depth + 20f);

            foreach (var label in Object.FindObjectsByType<BillboardLabel>(FindObjectsInactive.Exclude))
                label.Align(_cam);
        }
    }
}
