using Harmony12;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;
using static ModMaker.Extensions.RichText;

namespace DataViewer.Menus
{
    public class PatchesViewer : ModBase.Menu.ToggleablePage
    {
        private Dictionary<string, string> _modIdsToColor;
        private string _modId;
        private static Dictionary<MethodBase, List<Patch>> _patches = null;

        private GUIStyle _buttonStyle;

        public override string Name => "Patches";

        public override int Priority => 900;

        public override void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Core == null || !Core.Enabled)
                return;

            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };

            try
            {
                using (new GUILayout.HorizontalScope())
                {
                    // modId <=> color
                    using (new GUILayout.VerticalScope())
                    {
                        if (GUILayout.Button("Refresh List Of Patch Owners", _buttonStyle))
                        {
                            RefreshListOfPatchOwners(modEntry.Info.Id);
                        }

                        if (GUILayout.Button("Clear List Of Patch Owners", _buttonStyle))
                        {
                            _patches = null;
                            _modId = null;
                            _modIdsToColor = null;
                        }
                    }

                    // mod selection
                    if (_modIdsToColor != null)
                    {
                        using (new GUILayout.VerticalScope())
                        {
                            if (GUILayout.Button("None", _buttonStyle))
                            {
                                _patches = null;
                                _modId = null;
                            }

                            foreach (KeyValuePair<string, string> pair in _modIdsToColor)
                            {
                                if (GUILayout.Button(pair.Key.Color(pair.Value), _buttonStyle))
                                {
                                    _patches = null;
                                    _modId = pair.Key;
                                }
                            }
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                // info selection
                if(_modIdsToColor != null && !string.IsNullOrEmpty(_modId))
                {
                    GUILayout.Space(10f);

                    GUILayout.Label("Selected Patch Owner: " + _modId.Color(_modIdsToColor[_modId]));

                    GUILayout.Space(10f);

                    if (GUILayout.Button("Refresh Patch Info (All Mods)", _buttonStyle))
                    {
                        RefreshPatchInfoOfAllMods(_modId);
                    }

                    if (GUILayout.Button("Refresh Patch Info (Selected)", _buttonStyle))
                    {
                        RefreshPatchInfoOfSelected(_modId);
                    }

                    if (GUILayout.Button("Refresh Patch Info (Potential Conflict With ...)", _buttonStyle))
                    {
                        RefreshPatchInfoOfPotentialConflict(_modId);
                    }
                }

                // display info
                if (_modIdsToColor != null && !string.IsNullOrEmpty(_modId) && _patches != null)
                {
                    if (GUILayout.Button("Clear Patch Info", _buttonStyle))
                    {
                        _patches = null;
                        return;
                    }

                    foreach (KeyValuePair<MethodBase, List<Patch>> item in _patches)
                    {
                        GUILayout.Space(10f);

                        using (new GUILayout.VerticalScope())
                        {
                            GUILayout.Label(item.Key.DeclaringType.FullName + "." + item.Key.Name, _buttonStyle);

                            using (new GUILayout.HorizontalScope())
                            {
                                using (new GUILayout.VerticalScope())
                                {
                                    foreach (Patch patch in item.Value)
                                    {
                                        GUILayout.Label(patch.patch.Name, _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope())
                                {
                                    foreach (Patch patch in item.Value)
                                    {
                                        GUILayout.Label(patch.owner.Color(_modIdsToColor[patch.owner]), _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope())
                                {
                                    foreach (Patch patch in item.Value)
                                    {
                                        GUILayout.Label(patch.priority.ToString(), _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope())
                                {
                                    foreach (Patch patch in item.Value)
                                    {
                                        GUILayout.Label(patch.patch.DeclaringType.DeclaringType?.Name, _buttonStyle);
                                    }
                                }

                                using (new GUILayout.VerticalScope())
                                {
                                    foreach (Patch patch in item.Value)
                                    {
                                        GUILayout.Label(patch.patch.DeclaringType.Name, _buttonStyle);
                                    }
                                }

                                GUILayout.FlexibleSpace();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _patches = null;
                _modId = null;
                _modIdsToColor = null;
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }

        }

        private void RefreshListOfPatchOwners(string modId, bool reset = true)
        {
            if (reset || _modIdsToColor == null)
                _modIdsToColor = new Dictionary<string, string>();

            HarmonyInstance harmonyInstance = HarmonyInstance.Create(modId);
            foreach (Patch patch in harmonyInstance.GetPatchedMethods().SelectMany(method =>
            {
                Patches patches = harmonyInstance.GetPatchInfo(method);
                return patches.Prefixes.Concat(patches.Transpilers).Concat(patches.Postfixes);
            }))
            {
                if (!_modIdsToColor.ContainsKey(patch.owner))
                    _modIdsToColor[patch.owner] = ColorUtility.ToHtmlStringRGBA(UnityEngine.Random.ColorHSV(0f, 1f, 0.25f, 1f, 0.75f, 1f));
            }
        }

        private void RefreshPatchInfoOfAllMods(string modId)
        {
            _patches = new Dictionary<MethodBase, List<Patch>>();
            HarmonyInstance harmonyInstance = HarmonyInstance.Create(modId);
            foreach (MethodBase method in harmonyInstance.GetPatchedMethods())
            {
                _patches.Add(method, GetSortedPatches(harmonyInstance, method).ToList());
            }
        }

        private void RefreshPatchInfoOfSelected(string modId)
        {
            _patches = new Dictionary<MethodBase, List<Patch>>();
            HarmonyInstance harmonyInstance = HarmonyInstance.Create(_modId);
            foreach (MethodBase method in harmonyInstance.GetPatchedMethods())
            {
                IEnumerable<Patch> patches =
                    GetSortedPatches(harmonyInstance, method).Where(patch => patch.owner == _modId);
                if (patches.Any())
                {
                    _patches.Add(method, patches.ToList());
                }
            }
        }

        private void RefreshPatchInfoOfPotentialConflict(string modId)
        {
            _patches = new Dictionary<MethodBase, List<Patch>>();
            HarmonyInstance harmonyInstance = HarmonyInstance.Create(_modId);
            foreach (MethodBase method in harmonyInstance.GetPatchedMethods())
            {
                IEnumerable<Patch> patches = GetSortedPatches(harmonyInstance, method);
                if (patches.Any(patch => patch.owner == _modId) && patches.Any(patch => patch.owner != _modId))
                {
                    _patches.Add(method, patches.ToList());
                }
            }
        }

        private IEnumerable<Patch> GetSortedPatches(HarmonyInstance harmonyInstance, MethodBase method)
        {
            Patches patches = harmonyInstance.GetPatchInfo(method);
            return patches.Prefixes.OrderByDescending(patch => patch.priority)
                .Concat(patches.Transpilers.OrderByDescending(patch => patch.priority))
                .Concat(patches.Postfixes.OrderByDescending(patch => patch.priority));
        }
    }
}