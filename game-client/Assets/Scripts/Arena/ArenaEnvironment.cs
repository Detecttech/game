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
        private const float Pitch = 52f;
        private const float FieldOfView = 32f;
        // Fraction of the vertical frame reserved for the board, bottom-anchored — the
        // HUD's question/choice UI occupies roughly the top 45% of the screen, so the
        // board is framed into the bottom 55% rather than centered under it.
        private const float BoardScreenFraction = 0.55f;

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
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 160f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Rich saturated Clash Royale cartoon blue sky horizon
            camera.backgroundColor = new Color(0.35f, 0.78f, 0.98f);

            var camData = camera.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;

            return camera;
        }

        private static Light AcquireKeyLight()
        {
            Light key = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) { key = light; break; }
            }

            if (key == null)
            {
                var go = new GameObject("Key Light");
                key = go.AddComponent<Light>();
                key.type = LightType.Directional;
            }

            key.color = new Color(1.00f, 0.96f, 0.88f); // Warm sunlit golden key light
            key.intensity = 1.35f;
            key.shadows = LightShadows.None; // Crisp toon presentation without dynamic shadow blobs on tiles
            key.shadowStrength = 0f;
            key.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

            RenderSettings.sun = key;
            return key;
        }

        private static Light AcquireFillLight()
        {
            var go = new GameObject("Fill Light");
            var fill = go.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.72f, 0.82f, 1.00f); // Bright saturated sky fill
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
            return fill;
        }

        private static void ConfigureAmbient()
        {
            // Trilight with crisp sky, soft neutral equator, and lush grass ground bounce
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.56f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.68f, 0.28f); // Vibrant lush emerald grass bounce
            RenderSettings.ambientIntensity = 1.05f;
        }

        private static void ConfigureVolume(Camera camera)
        {
            var existing = Object.FindFirstObjectByType<Volume>();
            if (existing != null) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.0f;
            bloom.intensity.value = 0.65f;
            bloom.scatter.value = 0.75f;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = 4;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            // Clash-Royale punchy/saturated toon look
            colorAdjustments.saturation.value = 28f;
            colorAdjustments.contrast.value = 14f;
            colorAdjustments.postExposure.value = 0.12f;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.12f;
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

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void SetDimensions(int width, int height, float tileSize = 1.32f)
        {
            GridWidth = Mathf.Max(1, width);
            GridHeight = Mathf.Max(1, height);
            TileSize = tileSize;
            ApplyFraming();
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || Mathf.Abs(_cam.aspect - _lastAspect) > 0.005f)
            {
                ApplyFraming();
            }
        }

        public void ApplyFraming()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;

            _cam.fieldOfView = 32f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 200f;

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastAspect = _cam.aspect;

            float centerX = (GridWidth - 1) * TileSize * 0.5f;

            float pitch = 50f;
            float pitchRad = pitch * Mathf.Deg2Rad;
            float fovRad = _cam.fieldOfView * Mathf.Deg2Rad;
            float aspect = _cam.aspect > 0.05f ? _cam.aspect : (float)Screen.width / Mathf.Max(1, Screen.height);

            // Bounding extents including castle walls, towers, spotlights, and moat
            float zTop = (GridHeight - 0.5f) * TileSize + 2.2f;
            float zBot = -0.5f * TileSize - 1.7f;
            float zSpan = zTop - zBot;

            float xSpan = GridWidth * TileSize + 4.6f;

            // Safe vertical placement: North wall & towers cleanly below HUD, South wall above bottom screen edge
            float yTargetTop = 0.20f;
            float yTargetBot = -0.84f;
            float xTargetMax = 0.88f;

            float k = Mathf.Tan(fovRad * 0.5f);
            float sinP = Mathf.Sin(pitchRad);
            float cosP = Mathf.Cos(pitchRad);

            float fTop = (yTargetTop * k) / Mathf.Max(0.01f, sinP - yTargetTop * k * cosP);
            float fBot = (yTargetBot * k) / Mathf.Max(0.01f, sinP - yTargetBot * k * cosP);

            float distV = zSpan / Mathf.Max(0.01f, fTop - fBot);
            float distH = xSpan / Mathf.Max(0.01f, 2f * xTargetMax * k * aspect);

            float dist = Mathf.Max(distV, distH);

            // Center vertical framing safely between the target bounds
            float aimZ = 0.5f * (zTop + zBot) - 0.5f * dist * (fTop + fBot);

            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 lookAtPoint = new Vector3(centerX, 0f, aimZ);
            Vector3 position = lookAtPoint - forward * dist;

            transform.SetPositionAndRotation(position, rotation);

            foreach (var label in Object.FindObjectsByType<BillboardLabel>(FindObjectsInactive.Exclude))
                label.Align(_cam);
        }
    }
}
