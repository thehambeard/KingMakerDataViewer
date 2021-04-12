using DataViewer.Utility.ReflectionTree;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ModMaker.Utility.RichTextExtensions;
using ToggleState = ModMaker.Utility.GUIHelper.ToggleState;

namespace DataViewer.Utility {
    public class ReflectionTreeView {
        private Tree _tree;
        private Tree _filteredTree;


        private float _height;
        private bool _mouseOver;
        private GUIStyle _buttonStyle;
        private GUIStyle _valueStyle;

        private int _nodesCount;
        private int _startIndex;
        private int _skipLevels;
        private String searchText = "";
        private String searchTextLower = "";

        private Rect _viewerRect;
        public float DepthDelta { get; set; } = 30f;

        public int MaxRows { get; set; } = 20;

        public object Root => _tree.Root;

        public float TitleMinWidth { get; set; } = 300f;

        public ReflectionTreeView() { }

        public ReflectionTreeView(object root) {
            SetRoot(root);
        }

        public void Clear() {
            _tree = null;
        }

        public void SetRoot(object root) {
            if (_tree != null)
                _tree.SetRoot(root);
            else
                _tree = new Tree(root);

            _tree.RootNode.CustomFlags = (int)ToggleState.On;
        }
       
        public void OnGUI(bool drawRoot = true, bool collapse = false) {
            if (_tree == null)
                return;
            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, stretchHeight = true };
            if (_valueStyle == null)
                _valueStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, stretchHeight = true };

            int startIndexUBound = Math.Max(0, _nodesCount - MaxRows);

            // mouse wheel & fix scroll position
            if (Event.current.type == EventType.Layout) {
                if (startIndexUBound > 0) {
                    if (_mouseOver) {
                        var delta = Input.mouseScrollDelta;
                        if (delta.y > 0 && _startIndex > 0)
                            _startIndex--;
                        else if (delta.y < 0 && _startIndex < startIndexUBound)
                            _startIndex++;
                    }
                    if (_startIndex > startIndexUBound) {
                        _startIndex = startIndexUBound;
                    }
                }
                else {
                    _startIndex = 0;
                }
            }
            using (new GUILayout.VerticalScope()) {
                // toolbar
                Main.Log("    2a");
                using (new GUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Collapse", GUILayout.ExpandWidth(false))) {
                        collapse = true;
                        _skipLevels = 0;
                    }

                    if (GUILayout.Button("Refresh", GUILayout.ExpandWidth(false)))
                        _tree.RootNode.SetDirty();

                    GUILayout.Space(10f);

                    GUIHelper.AdjusterButton(ref _skipLevels, "Skip Levels:", 0);

                    GUILayout.Space(10f);

                    MaxRows = GUIHelper.AdjusterButton(MaxRows, "Max Rows:", 10);

                    GUILayout.Space(10f);

                    GUILayout.Label("Title Width:", GUILayout.ExpandWidth(false));
                    TitleMinWidth = GUILayout.HorizontalSlider(TitleMinWidth, 0f, Screen.width / 2, GUILayout.Width(100f));

                    GUILayout.Space(10f);

                    GUILayout.Label($"Scroll Pos: {_startIndex} / {startIndexUBound}", GUILayout.ExpandWidth(false));

                    GUILayout.Space(10f);
                    GUILayout.Label("Search", GUILayout.ExpandWidth(false));
                    GUIHelper.TextField(ref searchText, null);
                    searchTextLower = searchText.ToLower();
                    //GUILayout.FlexibleSpace();
                }
                // view
                using (new GUILayout.VerticalScope()) {
                    using (new GUILayout.ScrollViewScope(new Vector2(), GUIStyle.none, GUIStyle.none, GUILayout.Height(_height))) {
                        using (new GUILayout.HorizontalScope(GUI.skin.box)) {
                            // nodes
                            using (new GUILayout.VerticalScope()) {
                                _nodesCount = 0;
                                if (drawRoot)
                                    DrawNode(_tree.RootNode, 0, collapse);
                                else
                                    DrawChildren(_tree.RootNode, 0, collapse);
                            }

                            // scrollbar
//                            if (startIndexUBound > 0)
                                _startIndex = (int)GUILayout.VerticalScrollbar(_startIndex, MaxRows, 0f, _nodesCount, GUILayout.ExpandHeight(true));
                        }

                        // cache height
                        if (Event.current.type == EventType.Repaint) {
                            var mousePos = Event.current.mousePosition;
                            _mouseOver = _viewerRect.Contains(Event.current.mousePosition);
                            //Main.Log($"mousePos: {mousePos} rect: {_viewerRect} --> {_mouseOver}");
                            _viewerRect = GUILayoutUtility.GetLastRect();
                            _height = _viewerRect.height + 5f;
                        }
                    }
                }
            }
        }

        private void DrawNode(Node node, int depth, bool collapse) {
            ToggleState expanded = (ToggleState)node.CustomFlags;

            if (depth >= _skipLevels && !(collapse && depth > 0)) {
                _nodesCount++;

                if (_nodesCount > _startIndex && _nodesCount <= _startIndex + MaxRows) {
                    using (new GUILayout.HorizontalScope()) {
                        if (!node.hasChildren) {
                            expanded = ToggleState.None;
                        }
                        else if (node.CustomFlags == (int)ToggleState.None) {
                            expanded = ToggleState.Off;
                        }
                        node.CustomFlags = (int)expanded;

                        // title
                        GUILayout.Space(DepthDelta * (depth - _skipLevels));
                        var name = node.Name;
                        if (name.ToLower().Contains(searchTextLower))
                            name = name.Bold();
                        GUIHelper.ToggleButton(ref expanded,
                            GetPrefix(node.NodeType).Color(RGBA.grey) +
                            name + " : " + node.Type.Name.Color(
                                node.IsBaseType ? RGBA.grey :
                                node.IsGameObject ? RGBA.magenta :
                                node.IsEnumerable ? RGBA.cyan : RGBA.orange),
                            () => node.CustomFlags = (int)ToggleState.On,
                            () => node.CustomFlags = (int)ToggleState.Off,
                            _buttonStyle, GUILayout.ExpandWidth(false), GUILayout.MinWidth(TitleMinWidth));
                        ;

                        // value
                        Color originalColor = GUI.contentColor;
                        GUI.contentColor = node.IsException ? Color.red : node.IsNull ? Color.grey : originalColor;
                        GUILayout.TextArea(node.ValueText, _valueStyle);
                        GUI.contentColor = originalColor;

                        // instance type
                        if (node.InstType != null && node.InstType != node.Type)
                            GUILayout.Label(node.InstType.Name.Color(RGBA.yellow), _buttonStyle, GUILayout.ExpandWidth(false));
                    }
                }
            }

            if (collapse)
                node.CustomFlags = (int)ToggleState.Off;

            // children
            if (expanded.IsOn())
                DrawChildren(node, depth + 1, collapse);

            string GetPrefix(NodeType nodeType) {
                switch (nodeType) {
                    case NodeType.Component:
                        return "[c] ";
                    case NodeType.Item:
                        return "[i] ";
                    case NodeType.Field:
                        return "[f] ";
                    case NodeType.Property:
                        return "[p] ";
                    default:
                        return string.Empty;
                }
            }
        }

        private void DrawChildren(Node node, int depth, bool collapse) {
            if (node.IsBaseType)
                return;

            var matches = new List<Node> { };
            var remaining = new List<Node> { };
            foreach (Node child in node.GetItemNodes()) {
                if (searchText.Length > 0 && child.Name.ToLower().Contains(searchTextLower))
                    matches.Add(child);
                else
                    remaining.Add(child);
            }
            foreach (Node child in node.GetComponentNodes()) {
                if (searchText.Length > 0 && child.Name.ToLower().Contains(searchTextLower))
                    matches.Add(child);
                else
                    remaining.Add(child);
            }
            foreach (Node child in node.GetPropertyNodes()) {
                if (searchText.Length > 0 && child.Name.ToLower().Contains(searchTextLower))
                    matches.Add(child);
                else
                    remaining.Add(child);
            }
            foreach (Node child in node.GetFieldNodes()) {
                if (searchText.Length > 0 && child.Name.ToLower().Contains(searchTextLower))
                    matches.Add(child);
                else
                    remaining.Add(child);
            }
            foreach (Node child in matches) {
                DrawNode(child, depth, collapse);
            }
            foreach (Node child in remaining) {
                DrawNode(child, depth, collapse);
            }

        }
    }
}
