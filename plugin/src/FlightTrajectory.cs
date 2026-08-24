/*
 * DragonScreen - FlightTrajectory
 *
 * GLUE. Draws the predicted descent trajectory, its impact point, and the TARGET (a green X on the barge /
 * splashdown point) over the FLIGHT view - the map-view sibling of this is MapTrajectory. The crew asked to
 * see, without opening the map, where the vehicle is coming down and how far that is from the target
 * (user 2026-08-24, "green x on the centre of the barge so we can visually see how far off target we are").
 *
 * ---- IT READS THE SAME CACHED PATH THE MAP OVERLAY DOES ----
 * ImpactPredictor.UpdateMapTrajectory (driven once per ~0.5 s from FlightDriver) fills MapPath / MapImpact /
 * MapTarget with BODY-FIXED positions relative to the body centre. Both overlays consume that one cache, so
 * the flight view and the map can never disagree - and the integration is paid for once. Which vehicle's
 * profile is in the cache (booster vs Crew Dragon) is chosen by FlightDriver; this file only draws it.
 *
 * ---- SCREEN-SPACE GL OVER THE FLIGHT CAMERA, DRAWN ON TOP ----
 * Every point is projected with the flight camera's WorldToScreenPoint and drawn in pixel space with the
 * stock Hidden/Internal-Colored shader (ZTest Always), so the overlay sits on top of the scene rather than
 * being occluded by the ground it is predicting. Gated on NOT being in map view (MapTrajectory owns that)
 * and on a fresh cache, so a finished descent leaves no ghost line. All wrapped - a cosmetic overlay must
 * never take the flight down.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class FlightTrajectory
    {
        private const string Tag = "[DragonScreen] ";
        /// <summary>Hide the overlay if the cached path has not been refreshed within this long, seconds.</summary>
        private const double StaleAfterS = 3.0;

        private static Material material;
        private static Overlay overlay;
        private static bool visible;

        /// <summary>Component on the flight camera; its OnPostRender projects the cached path each frame.</summary>
        private sealed class Overlay : MonoBehaviour
        {
            internal Camera cam;

            private void OnPostRender()
            {
                if (!visible || material == null) return;
                try { Draw(); } catch (Exception e) { Debug.LogWarning(Tag + "flight overlay draw: " + e.Message); }
            }
        }

        // ------------------------------------------------------------------ lifecycle (FlightDriver)

        public static void Start()
        {
            try
            {
                if (material == null) material = MakeMaterial();

                Camera fc = FlightCameraRef();
                if (fc == null) return;                         // no flight camera yet - try again via Update
                if (overlay == null)
                {
                    overlay = fc.gameObject.AddComponent<Overlay>();
                    overlay.cam = fc;
                }
                visible = false;
                overlay.enabled = false;
            }
            catch (Exception e) { Debug.LogWarning(Tag + "flight overlay start: " + e.Message); }
        }

        public static void Destroy()
        {
            try { if (overlay != null) UnityEngine.Object.Destroy(overlay); }
            catch (Exception e) { Debug.LogWarning(Tag + "flight overlay destroy: " + e.Message); }
            overlay = null;
            material = null;
            visible = false;
        }

        /// <summary>Called every frame from FlightDriver. Shows the overlay only in flight view with a fresh path.</summary>
        public static void Update()
        {
            // The flight camera can arrive a frame or two after Start(); attach lazily if we missed it.
            if (overlay == null) { Start(); if (overlay == null) { visible = false; return; } }

            bool want = !global::MapView.MapIsEnabled
                        && ImpactPredictor.MapValid
                        && ImpactPredictor.MapBody != null
                        && Planetarium.GetUniversalTime() - ImpactPredictor.MapStampUt < StaleAfterS;

            visible = want;
            overlay.enabled = want;   // no OnPostRender at all when there is nothing to draw
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>Colours: the flown path and its impact cross in warning-red, the target cross in green.</summary>
        private static readonly Color PathColor = new Color(1.0f, 0.25f, 0.15f, 0.9f);
        private static readonly Color TargetColor = new Color(0.2f, 1.0f, 0.35f, 1.0f);

        private static void Draw()
        {
            Camera cam = overlay.cam;
            CelestialBody b = ImpactPredictor.MapBody;
            List<Vector3d> path = ImpactPredictor.MapPath;
            if (cam == null || b == null || path == null || path.Count < 2) return;

            Vector3d bp = b.position;

            GL.PushMatrix();
            material.SetPass(0);
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);

            // The predicted path, screen-projected. Break the line wherever a point falls behind the
            // camera (z <= 0) so a segment never wraps across the screen.
            GL.Color(PathColor);
            Vector3 prev = Vector3.zero;
            bool havePrev = false;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint((Vector3)(bp + path[i]));
                if (sp.z <= 0.0f) { havePrev = false; continue; }
                if (havePrev) { GL.Vertex3(prev.x, prev.y, 0f); GL.Vertex3(sp.x, sp.y, 0f); }
                prev = sp; havePrev = true;
            }

            // The predicted impact (red X) and the target / barge centre (green X).
            Vector3 impact = cam.WorldToScreenPoint((Vector3)(bp + ImpactPredictor.MapImpact));
            if (impact.z > 0.0f) { GL.Color(PathColor); Cross(impact, 9f); }

            Vector3 target = cam.WorldToScreenPoint((Vector3)(bp + ImpactPredictor.MapTarget));
            if (target.z > 0.0f) { GL.Color(TargetColor); Cross(target, 14f); Cross(target, 12f); }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>An X centred on a screen point, drawn as two diagonals inside the current GL.LINES block.</summary>
        private static void Cross(Vector3 sp, float half)
        {
            GL.Vertex3(sp.x - half, sp.y - half, 0f); GL.Vertex3(sp.x + half, sp.y + half, 0f);
            GL.Vertex3(sp.x - half, sp.y + half, 0f); GL.Vertex3(sp.x + half, sp.y - half, 0f);
        }

        // ------------------------------------------------------------------ helpers

        private static Camera FlightCameraRef()
        {
            if (FlightCamera.fetch != null && FlightCamera.fetch.mainCamera != null)
                return FlightCamera.fetch.mainCamera;
            return Camera.main;
        }

        /// <summary>The stock unlit vertex-coloured material, forced to draw on top (ZTest Always, no ZWrite).</summary>
        private static Material MakeMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            Material m = new Material(shader);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            m.SetInt("_ZWrite", 0);
            m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            return m;
        }
    }
}
