using HarmonyLib;
using ModMaker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;
using static ModMaker.Utility.RichTextExtensions;

namespace DataViewer.Menus {
    public class PatchesViewer : IMenuSelectablePage {
        private Dictionary<string, string> _modIdsToColor;
        private string _modId;
        private static Dictionary<MethodBase, List<Patch>> _patches = null;
        private GUIStyle _buttonStyle;
        public string Name => "Patches";
        public int Priority => 900;
        bool firstTime = true;
        public void OnGUI(UnityModManager.ModEntry modEntry) {
            if (Mod == null || !Mod.Enabled)
                return;

            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            if (firstTime) {
                RefreshListOfPatchOwners(modEntry.Info.Id);
                RefreshPatchInfoOfAllMods(modEntry.Info.Id);
                firstTime = false;
            }
            try {
                using (new GUILayout.HorizontalScope()) {
                    // modId <=> color
                    using (new GUILayout.VerticalScope()) {
                        if (GUILayout.Button("Refresh List Of Patch Owners", _buttonStyle)) {
                            RefreshListOfPatchOwners(modEntry.Info.Id);
                        }

                        if (GUILayout.Button("Clear List Of Patch Owners", _buttonStyle)) {
                            _patches = null;
                            _modId = null;
                            _modIdsToColor = null;
                        }
                    }

                    // mod selection
                    if (_modIdsToColor != null) {
                        using (new GUILayout.VerticalScope()) {
                            if (GUILayout.Button("All", _buttonStyle)) {
                                _patches = null;
                                _modId = null;
                                RefreshPatchInfoOfAllMods(modEntry.Info.Id);
                            }

                            foreach (KeyValuePair<string, string> pair in _modIdsToColor) {
                                if (GUILayout.Button(pair.Key.Color(pair.Value), _buttonStyle)) {
                                    _patches = null;
                                    _modId = pair.Key;
                                    RefreshPatchInfoOfSelected(_modId);
                                }
                            }
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                // info selection
                if (_modIdsToColor != null) {
                    GUILayout.Space(10f);

                    GUILayout.Label("Selected Patch Owner: " + (string.IsNullOrEmpty(_modId) ? "All" : _modId.Color(_modIdsToColor[_modId])));

                    GUILayout.Space(10f);

                    if (string.IsNullOrEmpty(_modId)) {
                        if (GUILayout.Button("Refresh Patch Info (All Mods)", _buttonStyle)) {
                            RefreshPatchInfoOfAllMods(_modId);
                        }
                    }
                    else {
                        if (GUILayout.Button("Refresh Patch Info (Selected)", _buttonStyle)) {
                            RefreshPatchInfoOfSelected(_modId);
                        }

                        if (GUILayout.Button("Refresh Patch Info (Potential Conflict With ...)", _buttonStyle)) {
                            RefreshPatchInfoOfPotentialConflict(_modId);
                        }
                    }
                }

                // display info
                if (_modIdsToColor != null && _patches != null) {
                    //if (GUILayout.Button("Clear Patch Info", _buttonStyle)) {
                    //    _patches = null;
                    //    return;
                    //}

                    foreach (KeyValuePair<MethodBase, List<Patch>> item in _patches) {
                        GUILayout.Space(10f);
                        using (new GUILayout.VerticalScope()) {
                            var method = item.Key;
                            string typeStr = method.DeclaringType.FullName;
                            var methodComponents = method.ToString().Split();
                            var returnTypeStr = methodComponents[0];
                            var methodName = methodComponents[1];
                            GUILayout.Label($"{typeStr.Grey().Italic()}\n{returnTypeStr.Bold()} {methodName.Bold()}");

                            using (new GUILayout.HorizontalScope()) {
                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in item.Value) {
                                        GUILayout.Label(patch.PatchMethod.Name, _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in item.Value) {
                                        GUILayout.Label(patch.owner.Color(_modIdsToColor[patch.owner]), _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in item.Value) {
                                        GUILayout.Label(patch.priority.ToString(), _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in item.Value) {

                                        GUILayout.Label(patch.PatchMethod.DeclaringType.DeclaringType?.Name ?? "---", _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope()) {
                                    foreach (Patch patch in item.Value) {
                                        GUILayout.Label(patch.PatchMethod.DeclaringType.Name, _buttonStyle);
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
                _modId = null;
                _modIdsToColor = null;
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }

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
            Harmony harmonyInstance = new Harmony(_modId);
            foreach (MethodBase method in Harmony.GetAllPatchedMethods()) {
                IEnumerable<Patch> patches =
                    GetSortedPatches(method).Where(patch => patch.owner == _modId);
                if (patches.Any()) {
                    _patches.Add(method, patches.ToList());
                }
            }
        }

        private void RefreshPatchInfoOfPotentialConflict(string modId) {
            _patches = new Dictionary<MethodBase, List<Patch>>();
            Harmony harmonyInstance = new Harmony(_modId);
            foreach (MethodBase method in Harmony.GetAllPatchedMethods()) {
                IEnumerable<Patch> patches = GetSortedPatches(method);
                if (patches.Any(patch => patch.owner == _modId) && patches.Any(patch => patch.owner != _modId)) {
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
