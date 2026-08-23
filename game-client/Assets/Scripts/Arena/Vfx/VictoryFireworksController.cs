using System.Collections;
using QuizBattle.Arena.Visuals;
using UnityEngine;

namespace QuizBattle.Arena.Vfx
{
    /// Spawns celebratory 3D fireworks particle bursts and rocket trails over the goal row
    /// when a player reaches the finish line, keeping the arena lively and spectacular while
    /// the winner spectates the remaining racers.
    public class VictoryFireworksController : MonoBehaviour
    {
        private float _spawnTimer = 0f;
        private float _goalZ;
        private float _width;

        private static readonly Color[] FireworkColors = new[]
        {
            new Color(1.00f, 0.84f, 0.00f), // Golden Yellow
            new Color(1.00f, 0.20f, 0.35f), // Radiant Ruby
            new Color(0.00f, 0.85f, 1.00f), // Neon Cyan
            new Color(0.20f, 1.00f, 0.40f), // Emerald Green
            new Color(0.85f, 0.35f, 1.00f), // Electric Violet
            new Color(1.00f, 0.55f, 0.10f), // Solar Orange
            new Color(0.95f, 0.95f, 1.00f), // Brilliant White
        };

        public static VictoryFireworksController Spawn(Vector3 arenaCenter, float width, float goalZ)
        {
            var existing = Object.FindFirstObjectByType<VictoryFireworksController>();
            if (existing != null) return existing;

            var go = new GameObject("VictoryFireworksController");
            go.transform.position = arenaCenter;
            var comp = go.AddComponent<VictoryFireworksController>();
            comp._width = width;
            comp._goalZ = goalZ;
            return comp;
        }

        private void Start()
        {
            // Initial celebratory volley
            float centerX = (_width - 1) * 0.5f;
            LaunchRocket(new Vector3(centerX, 0f, _goalZ));
            LaunchRocket(new Vector3(centerX - 1.8f, 0f, _goalZ + 0.3f));
            LaunchRocket(new Vector3(centerX + 1.8f, 0f, _goalZ + 0.3f));
        }

        private void Update()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = Random.Range(0.35f, 0.65f);
                float centerX = (_width - 1) * 0.5f;
                float xOffset = Random.Range(-_width * 0.45f, _width * 0.45f);
                float zOffset = Random.Range(-0.8f, 1.2f);
                Vector3 launchPos = new Vector3(centerX + xOffset, 0f, _goalZ + zOffset);
                LaunchRocket(launchPos);
            }
        }

        private void LaunchRocket(Vector3 startPos)
        {
            StartCoroutine(AnimateRocket(startPos));
        }

        private IEnumerator AnimateRocket(Vector3 startPos)
        {
            Color colorA = FireworkColors[Random.Range(0, FireworkColors.Length)];
            Color colorB = FireworkColors[Random.Range(0, FireworkColors.Length)];

            float apexHeight = Random.Range(3.2f, 5.2f);
            Vector3 apex = startPos + Vector3.up * apexHeight + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));

            // Rocket glowing tracer
            var rocket = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rocket.name = "Rocket";
            Destroy(rocket.GetComponent<Collider>());
            rocket.transform.position = startPos;
            rocket.transform.localScale = Vector3.one * 0.16f;
            var glowMat = ToonMaterialFactory.GlowInstance(colorA, 3.5f, 0.2f);
            rocket.GetComponent<Renderer>().sharedMaterial = glowMat;

            float duration = Random.Range(0.38f, 0.52f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rocket.transform.position = Vector3.Lerp(startPos, apex, Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }

            Vector3 burstPos = rocket != null ? rocket.transform.position : apex;
            if (rocket != null) Destroy(rocket);

            // Grand festive starburst explosion at apex
            ParticleFactory.Burst(burstPos, colorA, size: 0.26f, count: 32, speed: 4.8f, lifetime: 0.75f);
            ParticleFactory.Burst(burstPos, colorB, size: 0.18f, count: 24, speed: 3.0f, lifetime: 0.90f);
            ParticleFactory.Burst(burstPos, Color.white, size: 0.12f, count: 18, speed: 6.0f, lifetime: 0.45f);
        }
    }
}
