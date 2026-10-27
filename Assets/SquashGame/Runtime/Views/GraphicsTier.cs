// ===========================================================
// Assets/SquashGame/Runtime/Views/GraphicsTier.cs
// Batch 1 (2026-10-26): one Graphics setting instead of Quality + Venue,
// so the game runs smoothly on a slow laptop, on battery too.
//
//   High           the 3D crowd, soft shadows (2 cascades), the picture grade
//   Medium         the 3D crowd; hard shadows (1 cascade), no grade
//   Low            painted crowd, short hard shadows, 85% resolution
//   Battery saver  Low at 60% resolution, no shadows, no anti-aliasing or
//                  HDR, capped at 30 fps, the menu pictures at 4 fps
//                  (2026-10-27: "a low detailed mode so it wouldn't take out
//                  laptop batteries too fast")
//   Auto           picks from the graphics card (and Battery saver while
//                  unplugged), then the governor: below the frame rate it
//                  aims for, the resolution steps down (to 60%); with time
//                  to spare, back up
//
// The render pipeline asset is a copy made at startup (changing the
// project's own asset at run time would save into it in the editor).
// Presentation only: the sim never sees any of this.
// ===========================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SquashGame
{
    public enum GraphicsLevel { High = 0, Medium = 1, Low = 2, Saver = 3 }

    public static class GraphicsTier
    {
        /// <summary>The setting's choices (GameSettings.Graphics: 0 Auto, then the levels).</summary>
        public static readonly string[] Names = { "Auto", "High", "Medium", "Low", "Battery saver" };
        public static GraphicsLevel Current { get; private set; } = GraphicsLevel.High;
        public static bool Auto { get; private set; } = true;
        /// <summary>The resolution scale the level asks for (the governor may go lower in Auto).</summary>
        public static float BaseScale { get; private set; } = 1f;

        static UniversalRenderPipelineAsset _asset;
        /// <summary>The render pipeline's second renderer: the same, without ambient occlusion (PC_Renderer_Lite).</summary>
        public const int LiteRenderer = 1;

        /// <summary>What Auto picks on this machine: integrated or small graphics -> Low; mid-range -> Medium; else High.</summary>
        public static GraphicsLevel Detect()
        {
            if (Application.isBatchMode) return GraphicsLevel.High;          // the tests and their captures: the full picture
            int vram = SystemInfo.graphicsMemorySize;                       // MB
            string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
            bool integrated = gpu.Contains("intel") && !gpu.Contains("arc") || gpu.Contains("uhd") || gpu.Contains("iris") ||
                              (gpu.Contains("radeon") && (gpu.Contains("vega") || gpu.Contains("graphics") && !gpu.Contains("rx")));
            if (integrated || vram < 2000 || SystemInfo.processorCount <= 4) return GraphicsLevel.Low;
            if (vram < 4000) return GraphicsLevel.Medium;
            return GraphicsLevel.High;
        }

        public static bool OnBattery { get { return SystemInfo.batteryStatus == BatteryStatus.Discharging; } }

        /// <summary>Apply the setting (0 Auto, 1 High .. 4 Battery saver). The crowd and the lights follow at the next court
        /// build; resolution, shadows and the frame cap at once.</summary>
        public static void Apply(int setting)
        {
            Auto = setting <= 0;
            Current = Auto ? (OnBattery ? GraphicsLevel.Saver : Detect()) : (GraphicsLevel)Mathf.Clamp(setting - 1, 0, 3);
            VenueView.Detail = Current <= GraphicsLevel.Medium ? 2 : 1;
            VenueView.Post = Current == GraphicsLevel.High;
            BaseScale = Current == GraphicsLevel.Low ? 0.85f : Current == GraphicsLevel.Saver ? 0.6f : 1f;
            if (Application.isBatchMode) return;

            if (_asset == null && QualitySettings.renderPipeline is UniversalRenderPipelineAsset a)
            {
                _asset = Object.Instantiate(a);
                _asset.name = a.name + " (run)";
            }
            if (_asset != null)
            {
                _asset.renderScale = BaseScale;
                _asset.shadowDistance = Current == GraphicsLevel.Saver ? 0f : Current == GraphicsLevel.Low ? 14f : 28f;
                _asset.shadowCascadeCount = Current == GraphicsLevel.High ? 2 : 1;
                _asset.upscalingFilter = UpscalingFilterSelection.Linear;
                _asset.msaaSampleCount = Current >= GraphicsLevel.Low ? 1 : _asset.msaaSampleCount;      // no anti-aliasing on Low and Battery saver
                _asset.supportsHDR = Current != GraphicsLevel.Saver;
                QualitySettings.renderPipeline = _asset;
                GraphicsSettings.defaultRenderPipeline = _asset;
            }
            Governor.Reset();
        }

        /// <summary>Battery saver's frame cap (the biggest saving on a laptop: half the frames of 60).</summary>
        public const int SaverFps = 30;

        /// <summary>The menu cards' live pictures: 15 a second, 4 in Battery saver.</summary>
        public static float VignetteFps { get { return Current == GraphicsLevel.Saver ? 4f : 15f; } }

        /// <summary>The key light's shadow edge for the level (soft only on High).</summary>
        public static LightShadows KeyShadows { get { return Current == GraphicsLevel.Saver ? LightShadows.None : Current == GraphicsLevel.High ? LightShadows.Soft : LightShadows.Hard; } }

        /// <summary>Auto's resolution governor: steps the render scale to hold the frame rate (presentation only).</summary>
        public static class Governor
        {
            public const float Min = 0.6f, Step = 0.1f;
            static float _slow, _spare, _avg = 1f / 60f;
            static bool _wasOnBattery;
            public static float Scale { get; private set; } = 1f;

            public static void Reset() { Scale = BaseScale; _slow = 0; _spare = 0; _wasOnBattery = OnBattery; }

            /// <summary>Once a frame. The target: the display's refresh capped at 60 (a slow PC needn't chase 144).</summary>
            public static void Tick(float dt)
            {
                if (!Auto || _asset == null || dt <= 0 || dt > 0.5f) return;
                if (OnBattery != _wasOnBattery) { Profile.ProfileStore.Apply(Profile.ProfileStore.Current.Settings, true); return; }   // unplugged / plugged in
                _avg += (dt - _avg) * 0.05f;
                float target = 1f / Mathf.Min(60f, Mathf.Max(30f, (float)Screen.currentResolution.refreshRateRatio.value));
                if (_avg > target * 1.12f) { _slow += dt; _spare = 0; }
                else if (_avg < target * 0.75f) { _spare += dt; _slow = 0; }
                else { _slow = 0; _spare = 0; }
                if (_slow > 2f && Scale > Min + 1e-3f) { Scale = Mathf.Max(Min, Scale - Step); _slow = 0; _asset.renderScale = Scale; Debug.Log("[graphics] render scale down to " + Scale.ToString("0.0")); }
                else if (_spare > 8f && Scale < BaseScale - 1e-3f) { Scale = Mathf.Min(BaseScale, Scale + Step); _spare = 0; _asset.renderScale = Scale; Debug.Log("[graphics] render scale up to " + Scale.ToString("0.0")); }
            }
        }
    }
}
