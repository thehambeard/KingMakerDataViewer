using DataViewer.Utils;
using Kingmaker.Blueprints;
using ModMaker.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;
using static DataViewer.Main;

namespace DataViewer.Menus
{
    public class BlueprintViewer : ModBase.Menu.ToggleablePage
    {
        private string[] _targetNames;

        // tree view
        private ReflectionTreeView _treeView = new ReflectionTreeView();
        private int _targetIndex;
        private List<BlueprintScriptableObject> _target;

        // selection
        private bool _expanded = true;
        private float _height = 200f;
        private Vector2 _scrollPosition;

        // search bar
        private string _searchName;
        private string _searchGuid;

        private GUIStyle _buttonStyle;

        private LibraryScriptableObject _library 
            => typeof(ResourcesLibrary).GetFieldValue<LibraryScriptableObject>("s_LibraryObject");

        public override string Name => "Blueprints";

        public override int Priority => 100;

        public override void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Core == null || !Core.Enabled)
                return;

            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };

            try
            {
                if (_targetNames == null && _library != null)
                {
                    _targetNames = new string[] { "None" }.Concat(_library.GetAllBlueprints()
                        .Select(item => item.GetType()).Distinct().Select(type => type.FullName).OrderBy(name => name)).ToArray();
                }

                // combobox - button
                using (new GUILayout.HorizontalScope())
                {
                    GUIHelper.ToggleButton(ref _expanded, $"Current: {_targetNames[_targetIndex]}", _buttonStyle, GUILayout.ExpandWidth(false));

                    GUILayout.Space(10f);

                    GUILayout.Label($"Height:");
                    _height = GUILayout.HorizontalSlider(_height, 0f, Screen.height / 2, GUILayout.Width(100f));

                    GUILayout.FlexibleSpace();
                }

                // combobox - list
                if (_expanded)
                {
                    using (new GUILayout.HorizontalScope(GUI.skin.box, GUILayout.Width(650f), GUILayout.Height(_height)))
                    {
                        GUILayout.Space(30f);

                        using (var scrollView = new GUILayout.ScrollViewScope(_scrollPosition))
                        {
                            _scrollPosition = scrollView.scrollPosition;

                            GUIHelper.SelectionGrid(ref _targetIndex, _targetNames, 1, () =>
                            {
                                if (_targetIndex == 0)
                                {
                                    _target = null;
                                    _treeView.Clear();
                                }
                                else
                                {
                                    _target = _library.GetAllBlueprints()
                                        .Where(item => item.GetType().FullName == _targetNames[_targetIndex]).ToList();
                                    _treeView.SetTarget(_target);
                                }
                                _searchName = null;
                                _searchGuid = null;
                            }, _buttonStyle);
                        }
                    }
                }
                
                if (_targetIndex != 0)
                {
                    GUILayout.Space(10f);

                    // search bar
                    using (new GUILayout.HorizontalScope())
                    {
                        GUIHelper.InputField(ref _searchName, "Search Name:", 90f, true, () => {
                            _treeView.SetTarget(_target);
                            _searchGuid = null;
                        }, () =>
                        {
                            _treeView.SetTarget(_target.Where(item => item.name.Contains(_searchName)).ToList());
                            _searchGuid = null;
                        });
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        GUIHelper.InputField(ref _searchGuid, "Search GUID:", 90f, true, () => {
                            _treeView.SetTarget(_target);
                            _searchName = null;
                        }, () => {
                            _treeView.SetTarget(_target.Where(item => item.AssetGuid.Contains(_searchGuid)).ToList());
                            _searchName = null;
                        });
                    }

                    GUILayout.Space(10f);

                    // tree view
                    _treeView.OnGUI();
                }
            }
            catch (Exception e)
            {
                _targetIndex = 0;
                _treeView.Clear();
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }
        }

    }
}
