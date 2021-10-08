using HarmonyLib;
using ModKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;
using static ModKit.Utility.RichTextExtensions;

namespace DataViewer.Menus {
    public class PatchesViewer : IMenuSelectablePage {


        private Dictionary<string, string> _modIdsToColor;
        private string _modID;
        private static Dictionary<MethodBase, List<Patch>> _patches = null;
        private static Dictionary<MethodBase, List<Patch>> _disabled = new Dictionary<MethodBase, List<Patch>> { };
        private GUIStyle _buttonStyle;
        public string Name => "Patches";
        public int Priority => 900;
        bool firstTime = true;
        public void OnGUI(UnityModManager.ModEntry modEntry) {
            if (ModManager == null || !ModManager.Enabled)
                return;

            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            if (firstTime) {
                RefreshListOfPatchOwners(modEntry.Info.Id);
                RefreshPatchInfoOfAllMods(modEntry.Info.Id);
                firstTime = false;
            }
            try {
                var selectedPatchName = "All".bold();

                using (new GUILayout.HorizontalScope()) {
                    // modId <=> color
                    using (new GUILayout.VerticalScope()) {
                        if (GUILayout.Button("Refresh List Of Patch Owners", _buttonStyle)) {
                            RefreshListOfPatchOwners(modEntry.Info.Id);
                        }

                        //if (GUILayout.Button("Clear List Of Patch Owners", _buttonStyle)) {
                        //    _patches = null;
                        //    _modID = null;
                        //    _modIdsToColor = null;
                        //}
                    }

                    // mod selection
                    if (_modIdsToColor != null) {
                        using (new GUILayout.VerticalScope()) {
                            if (GUILayout.Button("All".bold(), _buttonStyle)) {
                                _patches = null;
                                _modID = null;
                                RefreshPatchInfoOfAllMods(modEntry.Info.Id);
                            }
                            foreach (KeyValuePair<string, string> pair in _modIdsToColor) {
                                if (GUILayout.Button(pair.Key.Color(pair.Value).bold(), _buttonStyle)) {
                                    _patches = null;
                                    _modID = pair.Key;
                                    RefreshPatchInfoOfSelected(_modID);
                                }
                            }
                        }

                        // info selection
                        using (new GUILayout.VerticalScope()) {
                            selectedPatchName = string.IsNullOrEmpty(_modID) ? "All".bold() : _modID.Color(_modIdsToColor[_modID]).bold();
                            if (GUILayout.Button($"Refresh Patch Info ({selectedPatchName})", _buttonStyle)) {
                                RefreshPatchInfoOfAllMods(_modID);
                            }
                            if (GUILayout.Button($"Potential Conflicts for ({selectedPatchName})", _buttonStyle)) {
                                RefreshPatchInfoOfPotentialConflict(_modID);
                            }
                        }
                    }
                    GUILayout.FlexibleSpace();
                }

                if (_modIdsToColor != null) {
                    GUILayout.Space(10f);
                    GUILayout.Label($"Selected Patch Owner: {selectedPatchName}");

                    GUILayout.Space(10f);
                }

                // display info
                if (_modIdsToColor != null && _patches != null) {
                    int index = 1;
                    var methodBases = _patches.Keys.Concat(_disabled.Keys).Distinct().OrderBy(m => m.Name);

                    UI.Space(15);
                    UI.Div();
                    UI.Space(10);
                    UI.Label("Patch Debug");
                    using (UI.HorizontalScope()) {
                        if (_patches.Sum(entry => entry.Value.Count()) > 0) {
                            UI.ActionButton("Disable All", () => {
                                var actions = new List<Action> { };
                                foreach (var method in methodBases) {
                                    var enabledPatches = EnabledPatchesForMethod(method);
                                    foreach (var patch in enabledPatches)
                                        actions.Add(() => EnablePatchForMethod(false, patch, method));
                                }
                                foreach (var action in actions) action();
                            });
                        }
                        if (_disabled.Sum(entry => entry.Value.Count()) > 0) {
                            UI.ActionButton("Enable All", () => {
                                var actions = new List<Action> { };
                                foreach (var method in methodBases) {
                                    var disabledPatches = DisabledPatchesForMethod(method);
                                    foreach (var patch in disabledPatches)
                                        actions.Add(() => EnablePatchForMethod(true, patch, method));
                                }
                                foreach (var action in actions) action();
                            });
                        }
                    }
                    foreach (var method in methodBases) {
                        UI.Space(15);
                        UI.Div();
                        UI.Space(10);
                        using (new GUILayout.VerticalScope()) {
                            string typeStr = method.DeclaringType.FullName;
                            var methodComponents = method.ToString().Split();
                            var returnTypeStr = methodComponents[0];
                            var methodName = methodComponents[1];
                            using (new GUILayout.HorizontalScope()) {
                                GUILayout.Label($"{index++}", GUI.skin.box, UI.AutoWidth());
                                UI.Space(10);
                                GUILayout.Label($"{returnTypeStr.Grey().Bold()} {methodName.Bold()}\t{typeStr.Grey().Italic()}");
                            }
                            var enabledPatches = EnabledPatchesForMethod(method);
                            var disabledPatches = DisabledPatchesForMethod(method);

                            // do some quick cleanup of disabled entries that have been re-enabled outside of here
                            var intersection = new HashSet<Patch>(disabledPatches);
                            intersection.IntersectWith(enabledPatches);
                            if (intersection.Count > 0) {
                                foreach (var dupe in intersection) {
                                    disabledPatches.Remove(dupe);
                                    _disabled[method] = disabledPatches;
                                }
                            }

                            var patches = enabledPatches.Concat(disabledPatches).OrderBy(p => p.owner).ToArray();
                            UI.Space(15);
                            using (new GUILayout.HorizontalScope()) {
                                UI.Space(50);
                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in patches) {
                                        bool enabled = enabledPatches.Contains(patch);
                                        if (ModKit.Private.UI.CheckBox("", enabled)) {
                                            EnablePatchForMethod(!enabled, patch, method);
                                        }
                                    }
                                }
                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in patches) {
                                        GUILayout.Label(patch.PatchMethod.Name, GUI.skin.label);
                                    }
                                }
                                UI.Space(10);
                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in patches) {
                                        GUILayout.Label(patch.owner.Color(_modIdsToColor[patch.owner]).bold(), GUI.skin.label);
                                    }
                                }
                                UI.Space(10);
                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in patches) {
                                        GUILayout.Label(patch.priority.ToString(), GUI.skin.label);
                                    }
                                }
                                UI.Space(10);
                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in patches) {
                                        GUILayout.Label(patch.PatchMethod.DeclaringType.DeclaringType?.Name ?? "---", GUI.skin.label);
                                    }
                                }
                                UI.Space(10);
                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in patches) {
                                        GUILayout.TextArea(patch.PatchMethod.DeclaringType.Name, GUI.skin.textField);
                                    }
                                }
                                GUILayout.FlexibleSpace();
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                _patches = null;
                _modID = null;
                _modIdsToColor = null;
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }

        }
        private List<Patch> EnabledPatchesForMethod(MethodBase method) {
            return _patches.GetValueOrDefault(method, new List<Patch> { });
        }
        private List<Patch> DisabledPatchesForMethod(MethodBase method) {
            return _disabled.GetValueOrDefault(method, new List<Patch> { });
        }
        private void EnablePatchForMethod(bool enabled, Patch patch, MethodBase method) {
            var enabledPatches = EnabledPatchesForMethod(method);
            var disabledPatches = DisabledPatchesForMethod(method);
            if (enabled) {
                enabledPatches.Add(patch);
                disabledPatches.Remove(patch);
                //Harmony harmony = new Harmony(patch.owner);
                //harmony.Patch(method,);
            }
            else {
                disabledPatches.Add(patch);
                enabledPatches.Remove(patch);
                //var harmony = new Harmony(patch.owner);
                //var patchProcessor = new PatchProcessor(harmony, method);
                //harmony.Unpatch(patch., patch.PatchMethod);
            }
            _patches[method] = enabledPatches;
            _disabled[method] = disabledPatches;
        }
        private void RefreshListOfPatchOwners(string modId, bool reset = true) {
            if (reset || _modIdsToColor == null)
                _modIdsToColor = new Dictionary<string, string>();
            var patches = Harmony.GetAllPatchedMethods().SelectMany(method => {
                Patches patchInfo = Harmony.GetPatchInfo(method);
                return patchInfo.Prefixes.Concat(patchInfo.Transpilers).Concat(patchInfo.Postfixes);
            });
            var owners = patches.Select(patchInfo => patchInfo.owner).Distinct().OrderBy(owner => owner);
            float hue = 0.0f;
            foreach (var owner in owners) {
                if (!_modIdsToColor.ContainsKey(owner)) {
                    var color = UnityEngine.Random.ColorHSV(
                            hue, hue,
                            0.25f, .75f,
                            0.75f, 1f
                            );
                    _modIdsToColor[owner] = ColorUtility.ToHtmlStringRGBA(color);
                    hue = (hue + 0.1f) % 1.0f;
                }
            }
        }

        private void RefreshPatchInfoOfAllMods(string modId) {
            _patches = new Dictionary<MethodBase, List<Patch>>();
            foreach (MethodBase method in Harmony.GetAllPatchedMethods()) {
                _patches.Add(method, GetSortedPatches(method).ToList());
            }
        }

        private void RefreshPatchInfoOfSelected(string modId) {
            _patches = new Dictionary<MethodBase, List<Patch>>();
            foreach (MethodBase method in Harmony.GetAllPatchedMethods()) {
                IEnumerable<Patch> patches =
                    GetSortedPatches(method).Where(patch => patch.owner == _modID);
                if (patches.Any()) {
                    _patches.Add(method, patches.ToList());
                }
            }
        }

        private void RefreshPatchInfoOfPotentialConflict(string modId) {
            _patches = new Dictionary<MethodBase, List<Patch>>();
            foreach (MethodBase method in Harmony.GetAllPatchedMethods()) {
                IEnumerable<Patch> patches = GetSortedPatches(method);
                var owners = patches.Select(patch => patch.owner).Distinct().ToHashSet();
                if (owners.Count > 1 && (_modID == null || owners.Contains(_modID))) {
                    _patches.Add(method, patches.ToList());
                }
            }
        }

        private IEnumerable<Patch> GetSortedPatches(MethodBase method) {
            Patches patches = Harmony.GetPatchInfo(method);
            return patches.Prefixes.OrderByDescending(patch => patch.priority)
                .Concat(patches.Transpilers.OrderByDescending(patch => patch.priority))
                .Concat(patches.Postfixes.OrderByDescending(patch => patch.priority));
        }
    }
}
