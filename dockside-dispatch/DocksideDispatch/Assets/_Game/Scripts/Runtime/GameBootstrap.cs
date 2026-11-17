using UnityEngine;
using UnityEngine.Rendering;

namespace Dockside
{
    /// <summary>Entry point placed in the Main scene: sets up camera, light and frame rate, then starts the game.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private Palette palette;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (palette == null)
            {
                Debug.LogWarning("GameBootstrap: no Palette assigned, using defaults.");
                palette = ScriptableObject.CreateInstance<Palette>();
            }
            Shapes.Init(palette);
            Analytics.Initialize();   // GameAnalytics, if keys are configured

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }

            // Directional light kept for any lit materials; the game's own shaders are unlit/flat.
            var sun = FindFirstObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = new GameObject("Sun Light").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = palette.sunLight;
            sun.intensity = 1.25f;
            // The Flat shader draws exact palette colours with no lighting, so shadow maps would be wasted work.
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = palette.ambient;
            RenderSettings.fog = false;

            var controller = gameObject.AddComponent<GameController>();
            controller.Init(palette, cam);
        }
    }
}
