using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManager = UnityModManagerNet.UnityModManager;

namespace DataViewer {

    static class ModUI {

        [HarmonyPatch(typeof(UnityModManager.UI), "Update")]
        internal static class UnityModManager_UI_Update_Patch {
            static Dictionary<int, float> scrollOffsets = new Dictionary<int, float> { };

            private static void Prepare(MethodBase original) {
                Main.Log($"{original} - {new StackTrace().ToString()}");
            }
            private static void Postfix(UnityModManager.UI __instance, ref Rect ___mWindowRect, ref Vector2[] ___mScrollPosition, ref int ___tabId) {
                // save these in case we need them inside the mod
                //Logger.Log($"Rect: {___mWindowRect}");
                Main.ummRect = ___mWindowRect;
                Main.ummWidth = ___mWindowRect.width;
                Main.ummTabID = ___tabId;
            }
        }
    }
}