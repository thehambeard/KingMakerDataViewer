using DataViewer;
using DataViewer.Utility;
using DataViewer.Utility.ReflectionTree;
using Kingmaker.Blueprints;
using ModKit;
using ModKit.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;
using Kingmaker.Utility;

namespace DataViewer.Menus {
    public class BlueprintViewer : IMenuSelectablePage {
        Settings settings => Main.settings;
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
        private int _searchChildIndex = 0;

        // search selection
        private ToggleState _searchExpanded;
        private float _searchWidth;
        private float _searchHeight = 200f;
        private Vector2 _searchScrollPosition;

        private GUIStyle _buttonStyle;
        public string Name => "Blueprints";
        public int Priority => 100;
        public void RefreshBPSearchData() {
            _filteredBPs = GetBlueprints();
            if (_filteredBPs != null)
                _treeView.SetRoot(_filteredBPs);
            else
                _treeView.Clear();
            _bpFields = Node.GetFields(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
            _bpProperties = Node.GetProperties(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
            _bpChildNames = _bpFields.Keys.Concat(_bpProperties.Keys).OrderBy(key => key).ToArray();
            _searchChildIndex = Array.IndexOf(_bpChildNames, settings.searchChildName);
            if (_searchChildIndex == -1) {
                settings.searchChildName = "name";
                _searchChildIndex = Array.IndexOf(_bpChildNames, settings.searchChildName);
            }
        }
        public void RefreshTypeNames() {
            _bpTypes = new Type[] { null }.Concat(GetBlueprints()
    .Select(bp => bp.GetType()).Distinct().OrderBy(type => type.Name)).ToArray();
            if (!_selectionSearchText.IsNullOrEmpty()) {
                _bpTypes = _bpTypes.Where(type => type == null ? true : StringExtensions.Matches(type.Name, _selectionSearchText)).ToArray();
            }
            _bpTypeNames = _bpTypes.Select(type => type?.Name).ToArray();
            _bpTypeNames[0] = "All";
            _bpTypes[0] = typeof(BlueprintScriptableObject);
            _bpTypeIndex = 0;
        }

        public bool Matches(object value, string searchText) {
            if (value is IEnumerable<object> enumerable) {
                var typeStr = searchText;
                var valueStr = "";
                if (searchText.Contains(':')) {
                    var split = searchText.Split(':');
                    typeStr = split[0];
                    valueStr = split[1];
                }
                foreach (var item in enumerable) {
                    if ((typeStr.Length == 0 || item.GetType().Name.ToLower().Contains(typeStr)) &&
                        (valueStr.Length == 0 || item.ToString().ToLower().Contains(valueStr))
                    ) return !settings.searchReversed;
                }
            }

            try { return (value?.ToString()?.ToLower().Contains(searchText) ?? false) != settings.searchReversed; }
            catch { return settings.searchReversed; }
        }
        public void UpdateSearchResults() {
            if (string.IsNullOrEmpty(settings.searchText)) {
                _treeView.SetRoot(_filteredBPs);
            }
            else {
                var searchText = settings.searchText.ToLower();
                if (_bpFields.TryGetValue(_bpChildNames[_searchChildIndex], out FieldInfo f))
                    _treeView.SetRoot(_filteredBPs.Where(bp => Matches(f.GetValue(bp), searchText)).ToList());
                else if (_bpProperties.TryGetValue(_bpChildNames[_searchChildIndex], out PropertyInfo p))
                    _treeView.SetRoot(_filteredBPs.Where(bp => {
                        try {
                            return Matches(p.GetValue(bp), searchText);
                        }
                        catch {
                            return false;
                        }
                    }).ToList());
            }
        }
#if false
        public bool Matches(object value, string[] terms) {
            if (value is IEnumerable<object> enumerable) {
                if (terms.All(t => enumerable.Any(e => e.ToString().ToLower().Contains((t)))))
                     return !_searchReversed;
            }

            try {
                var text = value?.ToString()?.ToLower();
                if (text == null) return _searchReversed;
                return terms.All(t => text.Contains(t)) != _searchReversed;
            }
            catch { return _searchReversed; }
        }
        public void UpdateSearchResults() {
            if (string.IsNullOrEmpty(_searchText)) {
                _treeView.SetRoot(_filteredBPs);
            }
            else {
                var searchText = _searchText.ToLower();
                var terms = searchText.Split(' ');
                if (_bpFields.TryGetValue(_bpChildNames[_searchIndex], out FieldInfo f))
                    _treeView.SetRoot(_filteredBPs.Where(bp => Matches(f.GetValue(bp), terms)).ToList());
                else if (_bpProperties.TryGetValue(_bpChildNames[_searchIndex], out PropertyInfo p))
                    _treeView.SetRoot(_filteredBPs.Where(bp => Matches(p.GetValue(bp), terms)).ToList());
            }
        }
#endif
        public void OnGUI(UnityModManager.ModEntry modEntry) {
            if (ModManager == null || !ModManager.Enabled)
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
                        RefreshBPSearchData();
                    }
                    using (new GUILayout.HorizontalScope()) {
                        bool isDirty = false;
                        // Blueprint Picker
                        using (new GUILayout.VerticalScope()) {
                            // Header and Search Field
                            bool blueprintListIsDirty = false;
                            UI.Div();
                            using (new GUILayout.HorizontalScope(GUILayout.Width(450))) {
                                // Header and Search Field
                                GUILayout.Label($"{_bpTypeNames[_bpTypeIndex]}".Cyan(), GUILayout.Width(300));

                                GUILayout.Space(10);
                                UI.ActionTextField(ref _selectionSearchText, s => blueprintListIsDirty = true, UI.MinWidth(150));
                            }
                            if (blueprintListIsDirty) RefreshTypeNames();
                            UI.Div();
                            // Blueprint Picker List
                            if (_bpsExpanded.IsOn()) {
                                using (var scrollView = new GUILayout.ScrollViewScope(_bpsScrollPosition, GUILayout.Width(450))) {
                                    _bpsScrollPosition = scrollView.scrollPosition;
                                    UI.ActionSelectionGrid(ref _bpTypeIndex, _bpTypeNames, 1, (typeIndex) => {
                                        RefreshBPSearchData();
                                        _filteredBPs = typeIndex == 0 ? GetBlueprints() : GetBlueprints().Where(item => item.GetType() == _bpTypes[typeIndex]).ToList();
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
                            UI.Div();
                            if (_bpChildNames.Length > 0) {
                                using (new GUILayout.HorizontalScope()) {
                                    // search bar
                                    GUILayout.Space(10f);

                                    // slelection - button
                                    using (new GUILayout.HorizontalScope()) {
                                        UI.ToggleButton(ref _searchExpanded, $"Search: {_bpChildNames[_searchChildIndex]}", _buttonStyle, GUILayout.ExpandWidth(false));

                                        // _searchText input
                                        GUILayout.Space(10);
                                        UI.ActionTextField(ref settings.searchText, (s) => isDirty = true, GUILayout.Width(450));
                                        GUILayout.Space(10f);

                                        if (UI.Toggle("By Excluding", ref settings.searchReversed, GUILayout.ExpandWidth(false))) isDirty = true;


                                        if (_searchExpanded.IsOn()) {
                                            GUILayout.Space(10f);
                                        }
                                    }
                                }
                            }
                            // Data Search Field Picker
                            if (_searchExpanded.IsOn()) {
                                // selection
                                UI.Div();
                                var availableWidth = Main.ummWidth - 550;
                                int xCols = (int)Math.Ceiling(availableWidth / 300);
                                UI.ActionSelectionGrid(ref _searchChildIndex, _bpChildNames, xCols, (childIndex) => {
                                    isDirty = true;
                                    settings.searchChildName = _bpChildNames[childIndex];
                                }, _buttonStyle, GUILayout.Width(availableWidth));

                                // cache width
                                if (Event.current.type == EventType.Repaint) {
                                    _searchWidth = GUILayoutUtility.GetLastRect().width + 65f;
                                }
                            }
                            // Do the search
                            if (isDirty) {
                                UpdateSearchResults();
                            }
                            UI.Div();
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
