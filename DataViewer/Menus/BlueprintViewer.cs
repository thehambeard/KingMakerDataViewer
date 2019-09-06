using DataViewer.Utility;
using DataViewer.Utility.ReflectionTree;
using Kingmaker.Blueprints;
using ModMaker;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;

namespace DataViewer.Menus
{
    public class BlueprintViewer : IMenuSelectablePage
    {
        // blueprint info
        private Type[] _bpTypes;
        private string[] _bpTypeNames;

        // tree view
        private ReflectionTreeView _treeView = new ReflectionTreeView();
        private int _bpTypeIndex;
        private List<BlueprintScriptableObject> _blueprints;

        // blueprint selection
        private bool _bpsExpanded = true;
        private float _bpsWidth;
        private float _bpsHeight = 200f;
        private Vector2 _bpsScrollPosition;

        // search
        private Dictionary<string, FieldInfo> _bpFields;
        private Dictionary<string, PropertyInfo> _bpProperties;
        private string[] _bpChildNames;

        private int _searchIndex;
        private bool _searchReversed;
        private string _searchText;

        // search selection
        private bool _searchExpanded;
        private float _searchWidth;
        private float _searchHeight = 200f;
        private Vector2 _searchScrollPosition;

        private GUIStyle _buttonStyle;

        private LibraryScriptableObject _library 
            => typeof(ResourcesLibrary).GetFieldValue<LibraryScriptableObject>("s_LibraryObject");

        public string Name => "Blueprints";

        public int Priority => 100;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Mod == null || !Mod.Enabled)
                return;

            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };

            try
            {
                // blueprint type 
                {
                    // refresh blueprint types
                    if (_bpTypeNames == null)
                    {
                        if (_library != null)
                        {
                            _bpTypes = new Type[] { null }.Concat(_library.GetAllBlueprints()
                                .Select(bp => bp.GetType()).Distinct().OrderBy(type => type.FullName)).ToArray();
                            _bpTypeNames = _bpTypes.Select(type => type?.FullName).ToArray();
                            _bpTypeNames[0] = "None";

                            _bpTypeIndex = 0;
                            _blueprints = null;
                            _treeView.Clear();
                        }
                        else
                        {
                            return;
                        }
                    }

                    // slelection - button
                    using (new GUILayout.HorizontalScope())
                    {
                        GUIHelper.ToggleButton(ref _bpsExpanded, $"Current: {_bpTypeNames[_bpTypeIndex]}", _buttonStyle, GUILayout.ExpandWidth(false));

                        if (_bpsExpanded)
                        {
                            GUILayout.Space(10f);

                            GUILayout.Label("Height:", GUILayout.ExpandWidth(false));
                            _bpsHeight = GUILayout.HorizontalSlider(_bpsHeight, 0f, Screen.height / 2, GUILayout.Width(100f), GUILayout.ExpandWidth(false));
                        }
                    }

                    // slelection - list
                    if (_bpsExpanded)
                    {
                        using (new GUILayout.HorizontalScope(GUI.skin.box, GUILayout.Width(_bpsWidth), GUILayout.Height(_bpsHeight)))
                        {
                            GUILayout.Space(30f);

                            using (var scrollView = new GUILayout.ScrollViewScope(_bpsScrollPosition))
                            {
                                _bpsScrollPosition = scrollView.scrollPosition;

                                GUIHelper.SelectionGrid(ref _bpTypeIndex, _bpTypeNames, 1, () =>
                                {
                                    _searchText = null;
                                    if (_bpTypeIndex == 0)
                                    {
                                        _bpFields = null;
                                        _bpProperties = null;
                                        _bpChildNames = null;
                                        _searchIndex = 0;

                                        _blueprints = null;
                                        _treeView.Clear();
                                    }
                                    else
                                    {
                                        _bpFields = BaseNode.GetFields(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
                                        _bpProperties = BaseNode.GetProperties(_bpTypes[_bpTypeIndex]).OrderBy(info => info.Name).ToDictionary(info => info.Name);
                                        _bpChildNames = _bpFields.Keys.Concat(_bpProperties.Keys).ToArray();
                                        _searchIndex = Array.IndexOf(_bpChildNames, "name");

                                        _blueprints = _library.GetAllBlueprints().Where(item => item.GetType() == _bpTypes[_bpTypeIndex]).ToList();
                                        _treeView.SetTarget(_blueprints);
                                    }
                                }, _buttonStyle, GUILayout.ExpandWidth(false));

                                // cache width
                                if (Event.current.type == EventType.Repaint)
                                {
                                    _bpsWidth = GUILayoutUtility.GetLastRect().width + 65f;
                                }
                            }
                        }
                    }
                }

                if (_bpTypeIndex != 0)
                {
                    // search bar
                    if (_bpChildNames.Length > 0)
                    {
                        GUILayout.Space(10f);

                        bool isDirty = false;

                        // slelection - button
                        using (new GUILayout.HorizontalScope())
                        {
                            GUIHelper.ToggleButton(ref _searchExpanded, $"Search: {_bpChildNames[_searchIndex]}", _buttonStyle, GUILayout.ExpandWidth(false));

                            GUILayout.Space(10f);

                            GUIHelper.ToggleButton(ref _searchReversed, "By Excluding", () => isDirty = true, () => isDirty = true, _buttonStyle, GUILayout.ExpandWidth(false));

                            if (_searchExpanded)
                            {
                                GUILayout.Space(10f);

                                GUILayout.Label("Height:", GUILayout.ExpandWidth(false));
                                _searchHeight = GUILayout.HorizontalSlider(_searchHeight, 0f, Screen.height / 2, GUILayout.Width(100f), GUILayout.ExpandWidth(false));
                            }
                        }

                        // slelection - list
                        if (_searchExpanded)
                        {
                            using (new GUILayout.HorizontalScope(GUI.skin.box, GUILayout.Width(_searchWidth), GUILayout.Height(_searchHeight)))
                            {
                                GUILayout.Space(30f);

                                using (var scrollView = new GUILayout.ScrollViewScope(_searchScrollPosition))
                                {
                                    _searchScrollPosition = scrollView.scrollPosition;

                                    // selection
                                    GUIHelper.SelectionGrid(ref _searchIndex, _bpChildNames, 1, () => isDirty = true, _buttonStyle, GUILayout.ExpandWidth(false));

                                    // cache width
                                    if (Event.current.type == EventType.Repaint)
                                    {
                                        _searchWidth = GUILayoutUtility.GetLastRect().width + 65f;
                                    }
                                }
                            }
                        }

                        // input
                        GUIHelper.TextField(ref _searchText, () => isDirty = true);

                        // do search
                        if (isDirty)
                        {
                            if (string.IsNullOrEmpty(_searchText))
                            {
                                _treeView.SetTarget(_blueprints);
                            }
                            else
                            {
                                if (_bpFields.TryGetValue(_bpChildNames[_searchIndex], out FieldInfo f))
                                    _treeView.SetTarget(_blueprints.Where(bp =>
                                    {
                                        try { return (f.GetValue(bp)?.ToString().Contains(_searchText) ?? false) != _searchReversed; }
                                        catch { return _searchReversed; }
                                    }).ToList());
                                else if (_bpProperties.TryGetValue(_bpChildNames[_searchIndex], out PropertyInfo p))
                                    _treeView.SetTarget(_blueprints.Where(bp =>
                                    {
                                        try { return (p.GetValue(bp)?.ToString().Contains(_searchText) ?? false) != _searchReversed; }
                                        catch { return _searchReversed; }
                                    }).ToList());
                            }
                        }
                    }

                    GUILayout.Space(10f);

                    // tree view
                    _treeView.OnGUI(true, false, false);
                }
            }
            catch (Exception e)
            {
                _bpTypeIndex = 0;
                _treeView.Clear();
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }
        }
    }
}
