using DataViewer.Utils.ReflectionTree;
using ModMaker.Utils;
using System;
using UnityEngine;
using static ModMaker.Extensions.RichText;

namespace DataViewer.Utils
{
    public class ReflectionTreeView
    {
        private enum CustomFlags : int
        {
            expanded = 0x00000001
        }

        private Tree _tree;
        private bool _dirty;

        private float _height;
        private bool _mouseOver;
        private GUIStyle _buttonStyle;

        private int _nodesCount;
        private int _startIndex;
        private int _skipLevels;

        public float DepthDelta { get; set; } = 30f;

        public int MaxRows { get; set; } = 20;

        public object Target => _tree.Root.Value;

        public float TitleMinWidth { get; set; } = 300f;

        public ReflectionTreeView() { }

        public ReflectionTreeView(object target)
        {
            SetTarget(target);
        }

        public void Clear()
        {
            _tree = null;
        }

        public void SetTarget(object target)
        {
            if (_tree != null)
                _tree.SetTarget(target);
            else
                _tree = new Tree(target);

            _tree.Root.CustomFlags |= (int)CustomFlags.expanded;

            _dirty = true;
        }

        public void OnGUI(bool drawRoot = true, bool collapse = false, bool update = false)
        {
            if (_tree == null)
                return;

            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, stretchHeight = true };

            int startIndexUBound = Math.Max(0, _nodesCount - MaxRows);

            // set update flag
            if (Event.current.type == EventType.Used)
            {
                if (_dirty)
                {
                    update = true;
                    _dirty = false;
                }
            }
            else
            {
                collapse = false;
                update = false;
            }

            // mouse wheel & fix scroll position
            if (Event.current.type == EventType.Layout)
            {
                if (startIndexUBound > 0)
                {
                    if (_mouseOver)
                    {
                        float wheel = Input.GetAxis("Mouse ScrollWheel");
                        if (wheel > 0 && _startIndex > 0)
                            _startIndex--;
                        else if (wheel < 0 && _startIndex < startIndexUBound)
                            _startIndex++;
                    }
                    if (_startIndex > startIndexUBound)
                    {
                        _startIndex = startIndexUBound;
                    }
                }
                else
                {
                    _startIndex = 0;
                }
            }

            using (new GUILayout.VerticalScope())
            {
                // toolbar
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Collapse", GUILayout.ExpandWidth(false)))
                    {
                        collapse = true;
                        _skipLevels = 0;
                    }

                    if (GUILayout.Button("Refresh", GUILayout.ExpandWidth(false)))
                        update = true;

                    GUILayout.Space(10f);

                    GUIHelper.AdjusterButton(ref _skipLevels, "Skip Levels:", 0);

                    GUILayout.Space(10f);

                    MaxRows = GUIHelper.AdjusterButton(MaxRows, "Max Rows:", 10);

                    GUILayout.Space(10f);

                    GUILayout.Label($"Title Width:", GUILayout.ExpandWidth(false));
                    TitleMinWidth = GUILayout.HorizontalSlider(TitleMinWidth, 0f, Screen.width / 2, GUILayout.Width(100f));

                    GUILayout.Space(10f);

                    GUILayout.Label($"Scroll Position: {_startIndex} / {startIndexUBound}", GUILayout.ExpandWidth(false));

                    //GUILayout.FlexibleSpace();
                }

                // view
                using (new GUILayout.ScrollViewScope(new Vector2(), GUIStyle.none, GUIStyle.none, GUILayout.Height(_height)))
                {
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        // nodes
                        using (new GUILayout.VerticalScope())
                        {
                            _nodesCount = 0;
                            if (drawRoot)
                                DrawNode(_tree.Root, 0, collapse, update);
                            else
                                DrawChildren(_tree.Root, 0, collapse, update);
                        }

                        // scrollbar
                        if (startIndexUBound > 0)
                            _startIndex = (int)GUILayout.VerticalScrollbar(_startIndex, MaxRows, 0f, _nodesCount, GUILayout.ExpandHeight(true));
                    }

                    // cache height
                    if (Event.current.type == EventType.Repaint)
                    {
                        Rect lastRect = GUILayoutUtility.GetLastRect();
                        _height = lastRect.height + 5f;
                        _mouseOver = lastRect.Contains(Event.current.mousePosition);
                    }
                }
            }
        }

        private void DrawNode(BaseNode node, int depth, bool collapse, bool update)
        {
            if (update)
                node.UpdateValue();
            
            bool expanded = (node.CustomFlags & (int)CustomFlags.expanded) != 0;

            if (depth >= _skipLevels && !(collapse && depth > 0))
            {
                _nodesCount++;

                if (_nodesCount > _startIndex && _nodesCount <= _startIndex + MaxRows)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        // title
                        GUILayout.Space(DepthDelta * (depth - _skipLevels));
                        GUIHelper.ToggleButton(ref expanded,
                            (node.IsChildComponent ? "[c] " : node.IsEnumItem ? "[i] " : 
                            node.IsField ? "[f] " : node.IsProperty ? "[p] " : string.Empty).Color(RGBA.grey) +
                            node.Name + " : " + node.Type.Name.Color(
                                node.IsGameObject ? RGBA.magenta : node.IsEnumerable ? RGBA.cyan : 
                                !node.IsBaseType ? RGBA.orange : RGBA.grey),
                            () => node.CustomFlags |= (int)CustomFlags.expanded,
                            () => node.CustomFlags &= ~(int)CustomFlags.expanded,
                            _buttonStyle, GUILayout.ExpandWidth(false), GUILayout.MinWidth(TitleMinWidth));

                        // value
                        Color originalColor = GUI.contentColor;
                        GUI.contentColor = node.IsException ? Color.red : node.IsNull ? Color.grey : originalColor;
                        GUILayout.TextArea(node.ValueText);
                        GUI.contentColor = originalColor;

                        // instance type
                        if (!node.IsNull && (node.Type != node.InstType || node.IsNullable))
                            GUILayout.Label((Nullable.GetUnderlyingType(node.InstType) ?? node.InstType).Name
                                .Color(RGBA.yellow), _buttonStyle, GUILayout.ExpandWidth(false));
                    }
                }
            }

            if (collapse)
                node.CustomFlags &= ~(int)CustomFlags.expanded;

            // children
            if (expanded)
                DrawChildren(node, depth + 1, collapse, update);
        }

        private void DrawChildren(BaseNode node, int depth, bool collapse, bool update)
        {
            foreach (BaseNode child in node.GetEnumNodes())
            {
                DrawNode(child, depth, collapse, update);
            }

            foreach (BaseNode child in node.GetComponentNodes())
            {
                DrawNode(child, depth, collapse, update);
            }

            foreach (BaseNode child in node.GetFieldNodes())
            {
                DrawNode(child, depth, collapse, update);
            }

            foreach (BaseNode child in node.GetPropertyNodes())
            {
                DrawNode(child, depth, collapse, update);
            }
        }
    }
}
