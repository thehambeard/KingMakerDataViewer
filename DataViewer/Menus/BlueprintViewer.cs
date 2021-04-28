using DataViewer.Utility;
using DataViewer.Utility.ReflectionTree;
using Kingmaker.Blueprints;
using HarmonyLib;
using ModKit;
using ModKit.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;
using ToggleState = ModKit.Utility.ToggleState;

namespace DataViewer.Menus {
    public class BlueprintViewer : IMenuSelectablePage {
        private static IEnumerable<SimpleBlueprint> _allBlueprints = null;
        public static IEnumerable<SimpleBlueprint> GetBlueprints() {
            if (_allBlueprints == null) {
                if (BlueprintLoader.Shared.LoadInProgress()) { return null; }
                else {
                    Main.Log($"calling BlueprintLoader.Load");
                    BlueprintLoader.Shared.Load((bps) => {
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
        private static IEnumerable<SimpleBlueprint> _filteredBPs = null;

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
        private bool _searchReversed;
        private string _searchText;

        // search selection
        private ToggleState _searchExpanded;
        private float _searchWidth;
        private float _searchHeight = 200f;
        private Vector2 _searchScrollPosition;

        private GUIStyle _buttonStyle;
        public string Name => "Blueprints";
        public int Priority => 100;
        public void RefreshBPSearchData() {
            _bpFields = Node.GetFields(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
            _bpProperties = Node.GetProperties(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
            _bpChildNames = _bpFields.Keys.Concat(_bpProperties.Keys).OrderBy(key => key).ToArray();
            _searchIndex = Array.IndexOf(_bpChildNames, "name");
        }
    public void RefreshTypeNames() {
            _bpTypes = new Type[] { null }.Concat(GetBlueprints()
    .Select(bp => bp.GetType()).Distinct().OrderBy(type => type.Name)).ToArray();
            if (_selectionSearchText != null && _selectionSearchText.Length > 0) {
                var searchTextLower = _selectionSearchText.ToLower();
                _bpTypes = _bpTypes.Where(type => type?.FullName.ToLower().Contains(searchTextLower) ?? false).ToArray();
            }
            _bpTypeNames = _bpTypes.Select(type => type?.Name).ToArray();
            _bpTypeNames[0] = "All";
            _bpTypes[0] = typeof(BlueprintScriptableObject);
            _bpTypeIndex = 0;
            _filteredBPs = GetBlueprints();
            if (_filteredBPs != null)
                _treeView.SetRoot(_filteredBPs);
            else
                _treeView.Clear();
            RefreshBPSearchData();
        }
        public void UpdateSearchResults() {
            if (string.IsNullOrEmpty(_searchText)) {
                _treeView.SetRoot(_filteredBPs);
            }
            else {
                var searchText = _searchText.ToLower();
                if (_bpFields.TryGetValue(_bpChildNames[_searchIndex], out FieldInfo f))
                    _treeView.SetRoot(_filteredBPs.Where(bp => {
                        try { return (f.GetValue(bp)?.ToString()?.ToLower().Contains(searchText) ?? false) != _searchReversed; }
                        catch { return _searchReversed; }
                    }).ToList());
                else if (_bpProperties.TryGetValue(_bpChildNames[_searchIndex], out PropertyInfo p))
                    _treeView.SetRoot(_filteredBPs.Where(bp => {
                        try { return (p.GetValue(bp)?.ToString()?.ToLower().Contains(searchText) ?? false) != _searchReversed; }
                        catch { return _searchReversed; }
                    }).ToList());
            }
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
                            GUILayout.Label("Blueprints".Orange().Bold() + " loading: " + BlueprintLoader.Shared.progress.ToString("P2").Cyan().Bold());
                            return;
                        }
                        RefreshTypeNames();
                    }
                    using (new GUILayout.HorizontalScope()) {
                        bool isDirty = false;
                        // Blueprint Picker
                        using (new GUILayout.VerticalScope()) {
                            // Header and Search Field
                            GUIHelper.Div();
                            using (new GUILayout.HorizontalScope(GUILayout.Width(450))) {
                                // Header and Search Field
                                GUILayout.Label($"{_bpTypeNames[_bpTypeIndex]}".Cyan(), GUILayout.Width(300));

                                GUILayout.Space(10);
                                GUIHelper.TextField(ref _selectionSearchText, () => isDirty = true, null, GUILayout.Width(150));
                                if (isDirty) {
                                    RefreshTypeNames();
                                }
                            }
                            GUIHelper.Div();
                            // Blueprint Picker List
                            if (_bpsExpanded.IsOn()) {
                                using (var scrollView = new GUILayout.ScrollViewScope(_bpsScrollPosition, GUILayout.Width(450))) {
                                    _bpsScrollPosition = scrollView.scrollPosition;
                                    GUIHelper.SelectionGrid(ref _bpTypeIndex, _bpTypeNames, 1, () => {
                                        _searchText = null;
                                        RefreshBPSearchData();
                                        _filteredBPs = _bpTypeIndex == 0 ? GetBlueprints() : GetBlueprints().Where(item => item.GetType() == _bpTypes[_bpTypeIndex]).ToList();
                                        ;
                                        _treeView.SetRoot(_filteredBPs);
                                    }, _buttonStyle, GUILayout.Width(450));

                                    // cache width
                                    if (Event.current.type == EventType.Repaint) {
                                        _bpsWidth = GUILayoutUtility.GetLastRect().width + 65f;
                                    }
                                }
                                //}
                            }
                        }

                        using (new GUILayout.VerticalScope(GUI.skin.box)) {
                            // Data Search Bar
                            GUIHelper.Div();
                            if (_bpChildNames.Length > 0) {
                                using (new GUILayout.HorizontalScope()) {
                                    // search bar
                                    GUILayout.Space(10f);

                                        // slelection - button
                                        using (new GUILayout.HorizontalScope()) {
                                            GUIHelper.ToggleButton(ref _searchExpanded, $"Search: {_bpChildNames[_searchIndex]}", _buttonStyle, GUILayout.ExpandWidth(false));

                                        // _searchText input
                                        GUILayout.Space(10);
                                        GUIHelper.TextField(ref _searchText, () => isDirty = true, null, GUILayout.Width(450));
                                        GUILayout.Space(10f);

                                        if (GUIHelper.Checkbox(ref _searchReversed, "By Excluding", _buttonStyle, GUILayout.ExpandWidth(false))) isDirty = true;


                                        if (_searchExpanded.IsOn()) {
                                            GUILayout.Space(10f); }
                                    }
                                }
                            }
                            // Data Search Field Picker
                            if (_searchExpanded.IsOn()) {
                                // selection
                                GUIHelper.Div();
                                var availableWidth = Main.ummWidth - 550;
                                int xCols = (int)Math.Ceiling(availableWidth / 300);
                                    GUIHelper.SelectionGrid(ref _searchIndex, _bpChildNames, xCols, () => isDirty = true, _buttonStyle, GUILayout.Width(availableWidth));

                                    // cache width
                                    if (Event.current.type == EventType.Repaint) {
                                        _searchWidth = GUILayoutUtility.GetLastRect().width + 65f;
                                    }
                            }
                            // Do the search
                            if (isDirty) {
                                UpdateSearchResults();
                            }
                            GUIHelper.Div();
                            // tree view
                            using (new GUILayout.VerticalScope()) {
                                _treeView.OnGUI(true, false);
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                _bpTypeIndex = 0;
//                _treeView.Clear();
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }
        }
    }
}
