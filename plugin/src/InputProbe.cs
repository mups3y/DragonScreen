/*
 * DragonScreen - InputProbe
 *
 * A LOGGING PROBE FOR ONE COLLIDER. Development scaffolding, off unless a cfg flag turns it on, and
 * deleted once the input path is settled.
 *
 * ---- IT EXISTS TO ANSWER ONE QUESTION THAT CANNOT BE REASONED OUT ----
 * Tundra ships exactly ONE collider on this prop, a box covering the whole console, and every button
 * and screen sits strictly INSIDE it (measured, not assumed - see docs/REAL_DRAGON_SCREENS.md).
 * Unity delivers OnMouseDown to the collider a ray strikes FIRST. So either:
 *
 *      the enclosing console box takes every hit   -> per-button colliders can never receive one,
 *                                                     and the input design has to change
 *      the inner collider wins                     -> a collider per button works, as MAS assumes
 *
 * Both are plausible and the answer decides the whole input layer. Guessing costs a rebuild and a
 * restart to find out; probing costs the same restart and RETURNS THE ANSWER. Instrument before
 * theorising - the rule this project has paid for repeatedly.
 *
 * ---- WHY MAS DOES NOT SETTLE IT ----
 * MAS attaches its click handler straight onto a prop's own collider (MASComponentColliderEvent.cs:247)
 * and never creates one, because ASET's props ship a collider PER BUTTON with nothing enclosing them.
 * Tundra's do not. So MAS proves OnMouseDown works on IVA props; it does not prove it works on a
 * collider nested inside a bigger one. That gap is exactly what this measures.
 */
using UnityEngine;

namespace DragonScreen
{
    public class InputProbe : MonoBehaviour
    {
        /// <summary>Which collider this is, so two probes can be told apart in the log.</summary>
        public string label = "?";

        private int downs;

        public void OnMouseEnter()
        {
            Debug.Log("[DragonScreen.probe] ENTER  " + label);
        }

        public void OnMouseDown()
        {
            downs++;
            Debug.Log("[DragonScreen.probe] DOWN   " + label + "   (" + downs + ")");
        }

        public void OnMouseExit()
        {
            Debug.Log("[DragonScreen.probe] EXIT   " + label);
        }
    }
}
