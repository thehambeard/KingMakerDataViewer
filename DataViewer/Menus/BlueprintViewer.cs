using DataViewer.Utility;
using DataViewer.Utility.ReflectionTree;
using Kingmaker.Blueprints;
using Harmony12;
using ModMaker;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;
using ToggleState = ModMaker.Utility.GUIHelper.ToggleState;

namespace DataViewer.Menus {
    public class BlueprintViewer : IMenuSelectablePage {
        private static IEnumerable<BlueprintScriptableObject> _allBlueprints = null;
        public static IEnumerable<BlueprintScriptableObject> GetBlueprints() {
            if (_allBlueprints == null) {
                if (BlueprintLoader.LoadInProgress()) { return null; }
                else {
                    Main.Log($"calling BlueprintLoader.Load");
                    BlueprintLoader.Load((bps) => {
                        _allBlueprints = bps;
                        Main.Log($"success got {bps.Count()} bluerints");
                    });
                    return null;
                }
            }
            return _allBlueprints;
        }

        // blueprint info
        private Type[] _bpTypes;
        private string[] _bpTypeNames;
        private static IEnumerable<BlueprintScriptableObject> _filteredBPs = null;

        // tree view
        private ReflectionTreeView _treeView = new ReflectionTreeView();
        private int _bpTypeIndex;

        // blueprint selection
        private ToggleState _bpsExpanded = ToggleState.On;
        private float _bpsWidth;
        private float _bpsHeight = 200f;
        private Vector2 _bpsScrollPosition;

        // search
        private string _selectionSearchText;

        private Dictionary<string, FieldInfo> _bpFields;
        private Dictionary<string, PropertyInfo> _bpProperties;
        private string[] _bpChildNames;

        private int _searchIndex;
        private ToggleState _searchReversed;
        private string _searchText;

        // search selection
        private ToggleState _searchExpanded;
        private float _searchWidth;
        private float _searchHeight = 200f;
        private Vector2 _searchScrollPosition;

        private GUIStyle _buttonStyle;
        public string Name => "Blueprints";
        public int Priority => 100;
        public void RefreshTypeNames() {
            _bpTypes = new Type[] { null }.Concat(GetBlueprints()
    .Select(bp => bp.GetType()).Distinct().OrderBy(type => type.FullName)).ToArray();
            if (_selectionSearchText != null && _selectionSearchText.Length > 0) {
                _bpTypes = _bpTypes.Where(type => type?.FullName.ToLower().Contains(_selectionSearchText) ?? false).ToArray();
            }
            _bpTypeNames = _bpTypes.Select(type => type?.FullName).ToArray();
            _bpTypeNames[0] = "None";
            _bpTypeIndex = 0;
            _filteredBPs = null;
            _treeView.Clear();
        }
        public void OnGUI(UnityModManager.ModEntry modEntry) {
            if (Mod == null || !Mod.Enabled)
                return;

            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };

            try {
                // blueprint type 
                {
                    // refresh blueprint types
                    if (_bpTypeNames == null) {
                        if (GetBlueprints() == null) {
                            GUILayout.Label("Blueprints".Orange().Bold() + " loading: " + BlueprintLoader.progress.ToString("P2").Cyan().Bold());
                            return;
                        }
                        RefreshTypeNames();
                    }
                    bool isDirty = false;
                    // slelection - button
                    using (new GUILayout.HorizontalScope()) {
                        GUIHelper.ToggleButton(ref _bpsExpanded, $"Current: {_bpTypeNames[_bpTypeIndex]}", _buttonStyle, GUILayout.ExpandWidth(false));

                        if (_bpsExpanded.IsOn()) {
                            GUILayout.Space(10f);

                            GUILayout.Label("Height:", GUILayout.ExpandWidth(false));
                            _bpsHeight = GUILayout.HorizontalSlider(_bpsHeight, 0f, Screen.height / 2, GUILayout.Width(100f), GUILayout.ExpandWidth(false));
                            // _searchText input
                            GUILayout.Space(10);
                            GUIHelper.TextField(ref _selectionSearchText, () => isDirty = true, null, GUILayout.Width(200));
                            if (isDirty) {
                                RefreshTypeNames();
                            }
                        }
                    }
                    // slelection - list
                    if (_bpsExpanded.IsOn()) {
                        using (new GUILayout.HorizontalScope(GUI.skin.box, GUILayout.Width(_bpsWidth), GUILayout.Height(_bpsHeight))) {
                            GUILayout.Space(30f);

                            using (var scrollView = new GUILayout.ScrollViewScope(_bpsScrollPosition)) {
                                _bpsScrollPosition = scrollView.scrollPosition;

                                GUIHelper.SelectionGrid(ref _bpTypeIndex, _bpTypeNames, 1, () => {
                                    _searchText = null;
                                    if (_bpTypeIndex == 0) {
                                        _bpFields = null;
                                        _bpProperties = null;
                                        _bpChildNames = null;
                                        _searchIndex = 0;

                                        _filteredBPs = null;
                                        _treeView.Clear();
                                    }
                                    else {
                                        _bpFields = Node.GetFields(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
                                        _bpProperties = Node.GetProperties(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
                                        _bpChildNames = _bpFields.Keys.Concat(_bpProperties.Keys).OrderBy(key => key).ToArray();
                                        _searchIndex = Array.IndexOf(_bpChildNames, "name");

                                        _filteredBPs = GetBlueprints().Where(item => item.GetType() == _bpTypes[_bpTypeIndex]).ToList();
                                        _treeView.SetRoot(_filteredBPs);
                                    }
                                }, _buttonStyle, GUILayout.ExpandWidth(false));

                                // cache width
                                if (Event.current.type == EventType.Repaint) {
                                    _bpsWidth = GUILayoutUtility.GetLastRect().width + 65f;
                                }
                            }
                        }
                    }
                }
                if (_bpTypeIndex != 0) {
                    // search bar
                    if (_bpChildNames.Length > 0) {
                        GUILayout.Space(10f);

                        bool isDirty = false;

                        // slelection - button
                        using (new GUILayout.HorizontalScope()) {
                            GUIHelper.ToggleButton(ref _searchExpanded, $"Search: {_bpChildNames[_searchIndex]}", _buttonStyle, GUILayout.ExpandWidth(false));

                            GUILayout.Space(10f);

                            GUIHelper.ToggleButton(ref _searchReversed, "By Excluding", () => isDirty = true, () => isDirty = true, _buttonStyle, GUILayout.ExpandWidth(false));

                            // _searchText input
                            GUILayout.Space(10);
                            GUIHelper.TextField(ref _searchText, () => isDirty = true, null, GUILayout.Width(200));

                            if (_searchExpanded.IsOn()) {
                                GUILayout.Space(10f);

                                GUILayout.Label("Height:", GUILayout.ExpandWidth(false));
                                _searchHeight = GUILayout.HorizontalSlider(_searchHeight, 0f, Screen.height / 2, GUILayout.Width(100f), GUILayout.ExpandWidth(false));
                            }
                        }
                        // slelection - list
                        if (_searchExpanded.IsOn()) {
                            using (new GUILayout.HorizontalScope(GUI.skin.box, GUILayout.Width(_searchWidth), GUILayout.Height(_searchHeight))) {
                                GUILayout.Space(30f);

                                using (var scrollView = new GUILayout.ScrollViewScope(_searchScrollPosition)) {
                                    _searchScrollPosition = scrollView.scrollPosition;

                                    // selection
                                    GUIHelper.SelectionGrid(ref _searchIndex, _bpChildNames, 1, () => isDirty = true, _buttonStyle, GUILayout.ExpandWidth(false));

                                    // cache width
                                    if (Event.current.type == EventType.Repaint) {
                                        _searchWidth = GUILayoutUtility.GetLastRect().width + 65f;
                                    }
                                }
                            }
                        }
                        // do search
                        if (isDirty) {
                            if (string.IsNullOrEmpty(_searchText)) {
                                _treeView.SetRoot(_filteredBPs);
                            }
                            else {
                                var searchText = _searchText.ToLower();
                                if (_bpFields.TryGetValue(_bpChildNames[_searchIndex], out FieldInfo f))
                                    _treeView.SetRoot(_filteredBPs.Where(bp => {
                                        try { return (f.GetValue(bp)?.ToString()?.ToLower().Contains(searchText) ?? false) != _searchReversed.IsOn(); }
                                        catch { return _searchReversed.IsOn(); }
                                    }).ToList());
                                else if (_bpProperties.TryGetValue(_bpChildNames[_searchIndex], out PropertyInfo p))
                                    _treeView.SetRoot(_filteredBPs.Where(bp => {
                                        try { return (p.GetValue(bp)?.ToString()?.ToLower().Contains(searchText) ?? false) != _searchReversed.IsOn(); }
                                        catch { return _searchReversed.IsOn(); }
                                    }).ToList());
                            }
                        }
                    }

                    GUILayout.Space(10f);
                    // tree view
                    _treeView.OnGUI(true, false);
                }
            }
            catch (Exception e) {
                _bpTypeIndex = 0;
                _treeView.Clear();
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }
        }
    }
}
