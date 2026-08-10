/*
 * DragonScreen - ScreenTouch
 *
 * Makes one IVA display a TOUCHSCREEN: turns a mouse click on the glass into a pixel coordinate on
 * the page that is drawn there.
 *
 * ---- PORTED FROM MAS, NOT INVENTED ----
 * assets/reference/AvionicsSystems-master/Source/MASComponentColliderAdvanced.cs:64-111 is the whole
 * technique, and it is worth stating why each line is there rather than just copying it:
 *
 *      InitBoxCollider (:64-71)   precompute hitCorner = center - size/2, invSize = 1/size, and the
 *                                 worldToLocal matrix. CACHED, because a click handler that rebuilt
 *                                 a matrix per event would be the allocation rule broken at the one
 *                                 place it is least visible.
 *      HitAt (:73-111)            InternalCamera.Instance -> ScreenPointToRay(Input.mousePosition)
 *                                 -> collider.Raycast -> worldToLocal.MultiplyPoint(hit.point)
 *                                 -> subtract the corner, multiply by invSize -> 0..1 per axis.
 *
 * `collider.Raycast` tests THIS collider only, which is why nothing else in the cabin can intercept
 * the ray. That is the property that makes it correct, not a detail.
 *
 * MIT licensed. MOARdVPlus is CC-BY-NC-SA and NOTHING may be taken from it - see ASSET_PROVENANCE.md.
 *
 * ---- WHICH TWO AXES ARE THE SURFACE ----
 * MAS needs a `hitTransformation` delegate because a prop's face can lie on any pair of axes. Ours
 * does not: the screen mesh measures 0.2844 x 0.0000 x 0.1561, so Y is EXACTLY flat and X/Z are the
 * face. u comes from X, v from Z. Hard-coded on measured evidence rather than made configurable -
 * a cfg field here would be a knob nobody could set correctly without the same measurement.
 *
 * ---- THE ZERO-THICKNESS TRAP ----
 * AddComponent<BoxCollider> auto-fits the mesh, and this mesh is FLAT: the fitted box would be zero
 * thick in Y, which makes invSize.y infinite and can make the raycast miss entirely. So the flat
 * axis is given a minimum thickness. The buttons do not need this - the probe measured theirs at
 * 0.0024 - but the screens do, and finding that out from a silently dead touchscreen would be a
 * long evening.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public class ScreenTouch : MonoBehaviour
    {
        /// <summary>Minimum collider thickness on a flat axis, in metres.</summary>
        private const float MinThickness = 0.004f;

        private ScreenPainter painter;
        private int pageW, pageH, index;

        private BoxCollider box;
        private Vector3 hitCorner, invSize;
        private Matrix4x4 worldToLocal = Matrix4x4.identity;
        private Camera ivaCamera;
        private bool ready;

        public void Configure(ScreenPainter target, int width, int height, int screenIndex)
        {
            painter = target;
            pageW = width; pageH = height; index = screenIndex;

            box = gameObject.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();

                // Fatten the flat axis. See the header: a zero-thick box divides by zero below and
                // can fail to be hit at all.
                Vector3 s = box.size;
                if (s.x < MinThickness) s.x = MinThickness;
                if (s.y < MinThickness) s.y = MinThickness;
                if (s.z < MinThickness) s.z = MinThickness;
                box.size = s;
            }

            // MASComponentColliderAdvanced.cs:64-71, cached once.
            Vector3 size = box.size;
            hitCorner = box.center - 0.5f * size;
            invSize = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
            worldToLocal = transform.worldToLocalMatrix;
            ready = true;

            Debug.Log("[DragonScreen] touch armed on screen " + index
                      + " collider size=" + size.ToString("F4")
                      + " centre=" + box.center.ToString("F4"));
        }

        public void OnMouseDown()
        {
            if (!ready || painter == null) return;

            // InternalCamera can be null between scenes and during staging - MAS guards this too
            // (MASFlightComputer.cs:1857). A touchscreen that throws during staging is worse than one
            // that ignores a click.
            if (InternalCamera.Instance == null) return;
            if (ivaCamera == null)
            {
                ivaCamera = InternalCamera.Instance.gameObject.GetComponent<Camera>();
                if (ivaCamera == null) return;
            }

            // The matrix is refreshed per click, not cached forever: the IVA moves with the vessel,
            // so a matrix captured at load is only valid where the capsule was then. Once per click
            // is free; once per frame would not be.
            worldToLocal = transform.worldToLocalMatrix;

            RaycastHit hit;
            Ray ray = ivaCamera.ScreenPointToRay(Input.mousePosition);
            if (!box.Raycast(ray, out hit, Mathf.Infinity)) return;

            Vector3 local = worldToLocal.MultiplyPoint(hit.point) - hitCorner;
            float u = local.x * invSize.x;
            float v = local.z * invSize.z;

            // Page space is TOP-LEFT origin with y down (GL.LoadPixelMatrix(0,w,h,0)), so v has to be
            // inverted to become a page y. WHETHER u ALSO NEEDS INVERTING IS NOT ASSUMED - the raw
            // normalised values are logged and a marker is drawn where we think the touch landed, so
            // one look settles it instead of an argument about handedness.
            float px = u * pageW;
            float py = (1f - v) * pageH;

            Debug.Log("[DragonScreen] touch screen " + index
                      + "  norm u=" + u.ToString("F3") + " v=" + v.ToString("F3")
                      + "  -> page " + px.ToString("F0") + "," + py.ToString("F0"));

            painter.Touch(px, py);
        }
    }
}
