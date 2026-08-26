// DragonScreen - InputProbe
// ---- IT EXISTS TO ANSWER ONE QUESTION THAT CANNOT BE REASONED OUT ----
// ---- WHY MAS DOES NOT SETTLE IT ----
using UnityEngine;

namespace DragonScreen
{
    public class InputProbe : MonoBehaviour
    {
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
