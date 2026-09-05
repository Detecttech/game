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

        private Rect GetBoardViewport(bool refreshHud = false)
        {
            if (_hud == null && (refreshHud || Time.unscaledTime >= _nextHudCheck))
            {
                _hud = Object.FindFirstObjectByType<QuizBattle.UI.HUD.HudController>();
                _nextHudCheck = Time.unscaledTime + 0.5f;
            }
            if (_hud != null && _hud.isActiveAndEnabled)
            {
                return _hud.GetBoardViewport(_cam);
            }
            var pixels = QuizBattle.UI.HUD.HudController.GetCameraPixelRect(_cam);
            var safe = _cam.targetTexture != null ? pixels : Screen.safeArea;
            var layout = QuizBattle.UI.HUD.HudController.CalculateLayout(pixels, safe, false, false, false);
            return QuizBattle.UI.HUD.HudController.NormalizeViewport(layout.Board, pixels);
        }

        public void ApplyFraming()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;
            ApplyFraming(GetBoardViewport(true));
        }

        public void ApplyFraming(Rect boardViewport)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;

            _cam.orthographic = true;
            _cam.nearClipPlane = 0.3f;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastAspect = _cam.aspect;
            _lastViewport = boardViewport;
            if (_lastViewport.width <= 0f || _lastViewport.height <= 0f) return;
            _lastGridMatrix = _gridTransform != null ? _gridTransform.localToWorldMatrix : Matrix4x4.identity;
            float centerX = (GridWidth - 1) * TileSize * 0.5f;
            float centerZ = (GridHeight - 1) * TileSize * 0.5f;
            float halfW = GridWidth * TileSize * 0.5f;
            float halfH = GridHeight * TileSize * 0.5f;
            var localCenter = new Vector3(centerX, 0f, centerZ);
            var center = _lastGridMatrix.MultiplyPoint3x4(localCenter);
            var rotation = (_gridTransform != null ? _gridTransform.rotation : Quaternion.identity) * Quaternion.Euler(50f, 0f, 0f);
            var inverseRotation = Quaternion.Inverse(rotation);
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 8; corner++)
            {
                var local = localCenter + new Vector3(
                                (corner & 1) == 0 ? -halfW - 0.45f : halfW + 0.45f,
                                (corner & 2) == 0 ? -0.12f : 2.6f,
                                (corner & 4) == 0 ? -halfH - 0.15f : halfH + 0.15f);
                var point = inverseRotation * (_lastGridMatrix.MultiplyPoint3x4(local) - center);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
            float aspect = _cam.aspect;
            if (aspect <= 0f) return;
            _cam.orthographicSize = Mathf.Max((max.y - min.y) / _lastViewport.height,
                                              (max.x - min.x) / (_lastViewport.width * aspect)) * 0.5f;
            var midpoint = (min + max) * 0.5f;
            float distance = 20f - min.z;
            var position = center + rotation * new Vector3(
                               midpoint.x - (_lastViewport.center.x * 2f - 1f) * _cam.orthographicSize * aspect,
                               midpoint.y - (_lastViewport.center.y * 2f - 1f) * _cam.orthographicSize, -distance);
            transform.SetPositionAndRotation(position, rotation);
            _cam.farClipPlane = Mathf.Max(200f, distance + max.z + 20f);

            foreach (var label in Object.FindObjectsByType<BillboardLabel>(FindObjectsInactive.Exclude))
                label.Align(_cam);
        }
    }
}
