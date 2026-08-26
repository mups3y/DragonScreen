/*
 * DragonScreen - MapTrajectory
 *
 * GLUE. Draws the predicted re-entry trajectory and its impact point in the MAP view, so the crew can
 * see where the capsule comes down without the Trajectories add-on installed.
 *
 * ---- ⛔ THIS IS THE ADD-ON'S OWN MAP RENDERER, PORTED ----
 * The line/crosshair meshes, the screen-facing "ribbon" edge and the ScaledSpace conversions are from
 * Trajectories' `src/Display/MapOverlay.cs` (GPL-3.0, same licence as this project). What differs is
 * the SOURCE of the points: Trajectories walks its own patch list; we read the single body-fixed
 * atmospheric path `ImpactPredictor.UpdateMapTrajectory` cached in `ImpactPredictor.MapPath`.
 *
 * ---- MAP ONLY, NEVER THE FLIGHT VIEW ----
 * The renderer lives on `PlanetariumCamera.Camera` (the map camera) and everything is gated on
 * `global::MapView.MapIsEnabled`, so nothing is ever drawn over the flight view - which is what the crew asked
 * for. It also hides itself when the cached path goes stale, so a finished entry leaves no ghost line.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class MapTrajectory
    {
        private const string Tag = "[DragonScreen] ";
        private const float LineWidth = 3.0f;
        private const int Layer2D = 31;
        private const int Layer3D = 24;
        /// <summary>Hide the line if the cached path has not been refreshed within this long, seconds.</summary>
        private const double StaleAfterS = 3.0;

        private static Material material;
        private static Renderer2 renderer2;
        private static bool visible;

        /// <summary>Component on the map camera; its OnPreRender rebuilds the meshes each map frame.</summary>
        private sealed class Renderer2 : MonoBehaviour
        {
            internal readonly List<GameObject> meshes = new List<GameObject>();

            internal void OnPreRender()
            {
                if (meshes != null) RenderMesh();
            }

            internal bool Visible
            {
                set
                {
                    for (int i = 0; i < meshes.Count; i++)
                    {
                        MeshRenderer mr = meshes[i].GetComponent<MeshRenderer>();
                        if (mr != null) mr.enabled = value;
                    }
                    enabled = value;
                }
            }
        }

        // ------------------------------------------------------------------ lifecycle (FlightDriver)

        public static void Start()
        {
            try
            {
                if (global::MapView.fetch != null && material == null)
                    material = global::MapView.fetch.orbitLinesMaterial;
                if (PlanetariumCamera.Camera == null) return;
                if (renderer2 == null)
                    renderer2 = PlanetariumCamera.Camera.gameObject.AddComponent<Renderer2>();
                visible = false;
                renderer2.Visible = false;
            }
            catch (Exception e) { Debug.LogWarning(Tag + "map overlay start: " + e.Message); }
        }

        public static void Destroy()
        {
            try
            {
                if (renderer2 != null)
                {
                    for (int i = 0; i < renderer2.meshes.Count; i++)
                        if (renderer2.meshes[i] != null) GameObject.Destroy(renderer2.meshes[i]);
                    renderer2.meshes.Clear();
                    GameObject.Destroy(renderer2);
                }
            }
            catch (Exception e) { Debug.LogWarning(Tag + "map overlay destroy: " + e.Message); }
            renderer2 = null;
            material = null;
            visible = false;
        }

        /// <summary>Called every frame from FlightDriver. Shows the line only in map view with a fresh path.</summary>
        public static void Update()
        {
            if (renderer2 == null || PlanetariumCamera.Camera == null) { visible = false; return; }
            if (material == null && global::MapView.fetch != null) material = global::MapView.fetch.orbitLinesMaterial;

            bool want = global::MapView.MapIsEnabled && ImpactPredictor.MapValid
                        && ImpactPredictor.MapBody != null
                        && Planetarium.GetUniversalTime() - ImpactPredictor.MapStampUt < StaleAfterS;

            if (want && !visible) { visible = true; renderer2.Visible = true; }
            else if (!want && visible) { visible = false; renderer2.Visible = false; }
        }

        // ------------------------------------------------------------------ mesh building

        private static void RenderMesh()
        {
            if (!visible || material == null || ImpactPredictor.MapBody == null) return;

            List<GameObject> meshes = renderer2.meshes;
            for (int i = 0; i < meshes.Count; i++) meshes[i].SetActive(false);

            CelestialBody body = ImpactPredictor.MapBody;
            List<Vector3d> path = ImpactPredictor.MapPath;
            if (path != null && path.Count >= 2)
            {
                Mesh line = NextMesh();
                InitMeshFromPath((Vector3)body.position, line, path, Color.red);

                Mesh impact = NextMesh();
                InitMeshCrosshair(body, impact, (Vector3)ImpactPredictor.MapImpact, Color.red);

                Mesh target = NextMesh();
                InitMeshCrosshair(body, target, (Vector3)ImpactPredictor.MapTarget, Color.green);
            }
        }

        /// <summary>First inactive mesh in the pool, or a new one appended to it.</summary>
        private static Mesh NextMesh()
        {
            List<GameObject> meshes = renderer2.meshes;
            GameObject found = null;
            for (int i = 0; i < meshes.Count; i++)
                if (!meshes[i].activeSelf) { meshes[i].SetActive(true); found = meshes[i]; break; }

            if (found == null)
            {
                found = new GameObject("DragonScreenMapLine");
                found.AddComponent<MeshFilter>();
                MeshRenderer mr = found.AddComponent<MeshRenderer>();
                mr.enabled = visible;
                mr.receiveShadows = false;
                meshes.Add(found);
            }
            found.layer = global::MapView.Draw3DLines ? Layer3D : Layer2D;
            found.GetComponent<Renderer>().sharedMaterial = material;
            return found.GetComponent<MeshFilter>().mesh;
        }

        /// <summary>A screen-facing ribbon segment. Ported verbatim from MapOverlay.MakeRibbonEdge.</summary>
        private static void MakeRibbonEdge(Vector3d prevPos, Vector3d edgeCenter, float width,
                                           Vector3[] vertices, int startIndex)
        {
            Camera camera = PlanetariumCamera.Camera;
            Vector3 start = camera.WorldToScreenPoint(ScaledSpace.LocalToScaledSpace(prevPos));
            Vector3 end = camera.WorldToScreenPoint(ScaledSpace.LocalToScaledSpace(edgeCenter));
            Vector3 segment = new Vector3(end.y - start.y, start.x - end.x, 0).normalized * (width * 0.5f);

            if (!global::MapView.Draw3DLines)
            {
                float dist = Screen.height / 2 + 0.01f;
                start.z = start.z >= 0.15f ? dist : -dist;
                end.z = end.z >= 0.15f ? dist : -dist;
            }

            Vector3 p0 = end + segment;
            Vector3 p1 = end - segment;
            if (global::MapView.Draw3DLines)
            {
                p0 = camera.ScreenToWorldPoint(p0);
                p1 = camera.ScreenToWorldPoint(p1);
            }

            vertices[startIndex + 0] = p0;
            vertices[startIndex + 1] = p1;

            if (!global::MapView.Draw3DLines && (start.z > 0) != (end.z > 0))
            {
                vertices[startIndex + 0] = vertices[startIndex + 1];
                if (startIndex >= 2) vertices[startIndex - 2] = vertices[startIndex - 1];
            }
        }

        /// <summary>Ribbon mesh from the body-relative path. Adapted from MapOverlay.InitMeshFromTrajectory.</summary>
        private static void InitMeshFromPath(Vector3 bodyPosition, Mesh mesh, List<Vector3d> path, Color color)
        {
            int n = path.Count;
            Vector3[] vertices = new Vector3[n * 2];
            Vector2[] uvs = new Vector2[n * 2];
            int[] triangles = new int[(n - 1) * 6];

            Vector3d bp = (Vector3d)bodyPosition;
            Vector3d prevMeshPos = path[0] + bp - (path[1] - path[0]);
            for (int i = 0; i < n; ++i)
            {
                Vector3d curMeshPos = path[i] + bp;
                MakeRibbonEdge(prevMeshPos, curMeshPos, LineWidth, vertices, i * 2);
                uvs[i * 2 + 0] = new Vector2(0.8f, 0);
                uvs[i * 2 + 1] = new Vector2(0.8f, 1);
                if (i > 0)
                {
                    int idx = (i - 1) * 6;
                    triangles[idx + 0] = (i - 1) * 2 + 0;
                    triangles[idx + 1] = (i - 1) * 2 + 1;
                    triangles[idx + 2] = i * 2 + 1;
                    triangles[idx + 3] = (i - 1) * 2 + 0;
                    triangles[idx + 4] = i * 2 + 1;
                    triangles[idx + 5] = i * 2 + 0;
                }
                prevMeshPos = curMeshPos;
            }

            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; ++i) colors[i] = color;

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
        }

        /// <summary>Crosshair mesh at a body-relative position. Ported from MapOverlay.InitMeshCrosshair.</summary>
        private static void InitMeshCrosshair(CelestialBody body, Mesh mesh, Vector3 position, Color color)
        {
            Vector3[] vertices = new Vector3[8];
            Vector2[] uvs = new Vector2[8];
            int[] triangles = new int[12];

            Vector3 camPos = ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position)
                             - (Vector3)body.position;

            double impactDistFromBody = position.magnitude;
            if (impactDistFromBody < 1.0) { mesh.Clear(); return; }
            double altitude = impactDistFromBody - body.Radius + 1200.0;   // lift off the ground in 3D
            position *= (float)((body.Radius + altitude) / impactDistFromBody);

            Vector3 crossV1 = Vector3.Cross(position, Vector3.right).normalized;
            Vector3 crossV2 = Vector3.Cross(position, crossV1).normalized;

            float crossThickness = Mathf.Min(LineWidth * 0.001f * Vector3.Distance(camPos, position), 6000.0f);
            float crossSize = crossThickness * 10.0f;

            vertices[0] = position - crossV1 * crossSize + crossV2 * crossThickness; uvs[0] = new Vector2(0.8f, 1);
            vertices[1] = position - crossV1 * crossSize - crossV2 * crossThickness; uvs[1] = new Vector2(0.8f, 0);
            vertices[2] = position + crossV1 * crossSize + crossV2 * crossThickness; uvs[2] = new Vector2(0.8f, 1);
            vertices[3] = position + crossV1 * crossSize - crossV2 * crossThickness; uvs[3] = new Vector2(0.8f, 0);
            triangles[0] = 0; triangles[1] = 1; triangles[2] = 3;
            triangles[3] = 0; triangles[4] = 3; triangles[5] = 2;

            vertices[4] = position - crossV2 * crossSize - crossV1 * crossThickness; uvs[4] = new Vector2(0.8f, 0);
            vertices[5] = position - crossV2 * crossSize + crossV1 * crossThickness; uvs[5] = new Vector2(0.8f, 1);
            vertices[6] = position + crossV2 * crossSize - crossV1 * crossThickness; uvs[6] = new Vector2(0.8f, 0);
            vertices[7] = position + crossV2 * crossSize + crossV1 * crossThickness; uvs[7] = new Vector2(0.8f, 1);
            triangles[6] = 4; triangles[7] = 5; triangles[8] = 7;
            triangles[9] = 4; triangles[10] = 7; triangles[11] = 6;

            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; ++i) colors[i] = color;

            for (int i = 0; i < vertices.Length; ++i)
                vertices[i] = global::MapView.Draw3DLines
                    ? (Vector3)ScaledSpace.LocalToScaledSpace(vertices[i] + (Vector3)body.position)
                    : Vector3.zero;   // crosshair only shows in 3D map mode, as in the add-on

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }
    }
}
