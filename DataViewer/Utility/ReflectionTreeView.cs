﻿using Kingmaker;
using DataViewer.Utility.ReflectionTree;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ModMaker.Utility.StringExtensions;
using static ModMaker.Utility.RichTextExtensions;
using ToggleState = ModMaker.Utility.ToggleState;

namespace DataViewer.Utility {
    public class ReflectionTreeView {
        private Tree _tree;

        private float _height;
        private bool _mouseOver;
        private GUIStyle _buttonStyle;
        private GUIStyle _valueStyle;

        private int _nodesCount;
        private int _startIndex;
        private int _skipLevels;
        private String searchText = "";
        private int matchCount = 0;
        private int visitCount = 0;
        private int searchDepth = 0;
        private int searchBreadth = 0;
        private void updateCounts(int matchCount, int visitCount, int depth, int breadth) { 
            this.matchCount = matchCount; 
            this.visitCount = visitCount;
            this.searchDepth = depth;
            this.searchBreadth = breadth;
        }

        private Rect _viewerRect;
        public float DepthDelta { get; set; } = 30f;

        public int MaxRows { get { return Main.settings.maxRows; } }

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

            _tree.RootNode.Expanded = ToggleState.On;
            NodeSearch.Shared.StartSearch(_tree.RootNode, searchText, updateCounts);
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

                    Main.settings.maxRows = GUIHelper.AdjusterButton(Main.settings.maxRows, "Max Rows:", 10);

                    GUILayout.Space(10f);

#if false
                    GUILayout.Label("Title Width:", GUILayout.ExpandWidth(false));
                    TitleMinWidth = GUILayout.HorizontalSlider(TitleMinWidth, 0f, Screen.width / 2, GUILayout.Width(100f));

                    GUILayout.Space(10f);
#endif
                    GUILayout.Label($"Scroll: {_startIndex} / {startIndexUBound}", GUILayout.ExpandWidth(false));

                    GUILayout.Space(10f);
                    GUILayout.Label("Search", GUILayout.ExpandWidth(false));
                    GUIHelper.TextField(ref searchText, () => {
                        searchText = searchText.Trim();
                        NodeSearch.Shared.StartSearch(_tree.RootNode, searchText, updateCounts);
                    }, null, GUILayout.Width(250));
                    GUILayout.Space(10f);
                    if (GUILayout.Button("Stop", GUILayout.ExpandWidth(false))) {
                        NodeSearch.Shared.Stop();
                    }
                    GUILayout.Space(10f);
                    if (visitCount > 0) {
                        GUILayout.Label($"found {matchCount}".Cyan() + $" visited: {visitCount} (d: {searchDepth} b: {searchBreadth})".Orange());
                    }
                    GUILayout.FlexibleSpace();
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
            ToggleState expanded = node.Expanded;
            if ((searchText.Length > 0)
                && (node.ChildrenContainingMatches.Count == 0)
                && !node.Matches
                && node.GetParent()?.Expanded == ToggleState.Off) {
                return;
            }

            // the following isn't right.  We need to implement storing a list of matching children in the node
            //if (searchText.Length > 0 && node != _tree.RootNode && !node.Name.Matches(searchText) && !node.ValueText.Matches(searchText))
            //    return;
                if (depth >= _skipLevels && !(collapse && depth > 0)) {
                _nodesCount++;

                if (_nodesCount > _startIndex && _nodesCount <= _startIndex + MaxRows) {
                    using (new GUILayout.HorizontalScope()) {
                        if (!node.hasChildren) {
                            expanded = ToggleState.None;
                        }
                        else if (node.Expanded == ToggleState.None) {
                            expanded = ToggleState.Off;
                        }
                        node.Expanded = expanded;

                        // title
                        GUILayout.Space(DepthDelta * (depth - _skipLevels));
                        var name = node.Name;
                        var instText = ""; if (node.InstanceID is int instID) instText = "@" + instID.ToString();                        if (node.Matches) name = name.MarkedSubstring(searchText);
                        GUIHelper.ToggleButton(ref expanded,
                            GetPrefix(node.NodeType).Color(RGBA.grey) +
                            name + " : " + node.Type.Name.Color(
                                node.IsBaseType ? RGBA.grey :
                                node.IsGameObject ? RGBA.magenta :
                                node.IsEnumerable ? RGBA.cyan : RGBA.orange)
                            + instText,
                            () => node.Expanded = ToggleState.On,
                            () => node.Expanded = ToggleState.Off,
                            _buttonStyle, GUILayout.ExpandWidth(false), GUILayout.MinWidth(TitleMinWidth));

                        // value
                        Color originalColor = GUI.contentColor;
                        GUI.contentColor = node.IsException ? Color.red : node.IsNull ? Color.grey : originalColor;
                        GUILayout.TextArea(node.ValueText.MarkedSubstring(searchText), _valueStyle);
                        GUI.contentColor = originalColor;

                        // instance type
                        if (node.InstType != null && node.InstType != node.Type)
                            GUILayout.Label(node.InstType.Name.Color(RGBA.yellow), _buttonStyle, GUILayout.ExpandWidth(false));
                    }
#if false
                    using (new GUILayout.HorizontalScope()) {
                        // parent
                        try {
                            GUILayout.Space(DepthDelta * (depth - _skipLevels));
                            var parent = node.GetParent();
                            var parentText = "";
                            while (parent != null) {
                                parentText = parentText + parent.Name + " : " + parent.Type.Name;
                                try {
                                    parent = parent.GetParent();
                                }
                                catch (Exception e) {
                                    parentText += e.ToString();
                                    parent = null;
                                }

                            }
                            GUILayout.Label(parentText);
                        }
                        catch (Exception e) { }
                    }
#endif
                }
            }

            if (collapse)
                node.Expanded = ToggleState.Off;

            // children
            if (expanded.IsOn() || node.ChildrenContainingMatches.Count > 0)
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
            var matches = node.ChildrenContainingMatches;
            foreach (var child in matches) { DrawNode(child, depth, collapse); }
            foreach (var child in node.GetItemNodes()) { if (!matches.Contains(child)) DrawNode(child, depth, collapse); }
            foreach (var child in node.GetComponentNodes()) { if (!matches.Contains(child)) DrawNode(child, depth, collapse); }
            foreach (var child in node.GetPropertyNodes()) { if (!matches.Contains(child)) DrawNode(child, depth, collapse); }
            foreach (var child in node.GetFieldNodes()) { if (!matches.Contains(child)) DrawNode(child, depth, collapse); }
        }
    }
}
