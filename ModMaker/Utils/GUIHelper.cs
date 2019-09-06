using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ModMaker.Extensions.RichText;

namespace ModMaker.Utils
{
    public static class GUIHelper
    {
        public static string FormatOn = "◑".Color(RGBA.lime) + " - {0}";
        public static string FormatOff = "◐".Color(RGBA.red) + " - {0}";

        public static string GetToggleText(bool toggle, string text)
        {
            return string.Format(toggle ? FormatOn : FormatOff, text);
        }

        public static int AdjusterButton(int value, string text, int min = int.MinValue, int max = int.MaxValue)
        {
            AdjusterButton(ref value, text, min, max);
            return value;
        }

        public static void AdjusterButton(ref int value, string text, int min = int.MinValue, int max = int.MaxValue)
        {
            GUILayout.Label(text, GUILayout.ExpandWidth(false));
            if (GUILayout.Button("-", GUILayout.ExpandWidth(false)) && value > min)
                value--;
            GUILayout.Label(value.ToString(), GUILayout.ExpandWidth(false));
            if (GUILayout.Button("+", GUILayout.ExpandWidth(false)) && value < max)
                value++;
        }

        public static void InputField(ref string value, string text, float titleMinWidth, bool expandTextField, Action onClear, Action onChanged)
        {
            GUILayout.Label(text, GUILayout.MinWidth(titleMinWidth), GUILayout.ExpandWidth(false));
            string old = value;
            value = GUILayout.TextField(value, GUILayout.ExpandWidth(expandTextField));
            if (value != old)
            {
                if (string.IsNullOrEmpty(value))
                    onClear();
                else
                    onChanged();
            }
        }

        public static bool ToggleButton(bool toggle, string text, GUIStyle style = null, params GUILayoutOption[] options)
        {
            ToggleButton(ref toggle, text, style, options);
            return toggle;
        }

        public static void ToggleButton(ref bool toggle, string text, GUIStyle style = null, params GUILayoutOption[] options)
        {
            if (GUILayout.Button(GetToggleText(toggle, text), style ?? GUI.skin.button, options))
                toggle = !toggle;
        }

        public static bool ToggleButton(bool toggle, string text, Action on, Action off, GUIStyle style = null, params GUILayoutOption[] options)
        {
            ToggleButton(ref toggle, text, on, off, style, options);
            return toggle;
        }

        public static void ToggleButton(ref bool toggle, string text, Action on, Action off, GUIStyle style = null, params GUILayoutOption[] options)
        {
            bool old = toggle;
            ToggleButton(ref toggle, text, style, options);
            if (toggle != old)
            {
                if (toggle)
                    on?.Invoke();
                else
                    off?.Invoke();
            }
        }

        public static void ToggleButton(ref bool toggle, string text, ref float minWidth, GUIStyle style = null, params GUILayoutOption[] options)
        {
            GUIContent content = new GUIContent(GetToggleText(toggle, text));
            style = style ?? GUI.skin.button;
            minWidth = Math.Max(minWidth, style.CalcSize(content).x);
            if (GUILayout.Button(content, style, options?.Concat(new[] { GUILayout.Width(minWidth) }).ToArray() ?? new[] { GUILayout.Width(minWidth) }))
                toggle = !toggle;
        }

        public static void ToggleButton(ref bool toggle, string text, ref float minWidth, Action on, Action off, GUIStyle style = null, params GUILayoutOption[] options)
        {
            bool old = toggle;
            ToggleButton(ref toggle, text, ref minWidth, style, options);
            if (toggle != old)
            {
                if (toggle)
                    on?.Invoke();
                else
                    off?.Invoke();
            }
        }

        public static bool ToggleTypeList(bool toggle, string text, HashSet<string> selectedTypes, HashSet<Type> allTypes, GUIStyle style = null, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal();

            ToggleButton(ref toggle, text, style, options);

            if (toggle)
            {
                using(new GUILayout.VerticalScope())
                {
                    using(new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select All"))
                        {
                            foreach (Type type in allTypes)
                            {
                                selectedTypes.Add(type.FullName);
                            }
                        }
                        if (GUILayout.Button("Deselect All"))
                        {
                            selectedTypes.Clear();
                        }
                    }

                    foreach (Type type in allTypes)
                    {
                        bool selected = selectedTypes.Contains(type.FullName);
                        ToggleButton(ref selected, type.Name.ToSentence(),
                            () => selectedTypes.Add(type.FullName),
                            () => selectedTypes.Remove(type.FullName),
                            style, options);
                    }
                }
            }

            GUILayout.EndHorizontal();

            return toggle;
        }

        public static void Toolbar(ref int selected, string[] texts, GUIStyle style = null, params GUILayoutOption[] options)
        {
            selected = GUILayout.Toolbar(selected, texts, style ?? GUI.skin.button, options);
        }

        public static void SelectionGrid(ref int selected, string[] texts, int xCount, GUIStyle style = null, params GUILayoutOption[] options)
        {
            selected = GUILayout.SelectionGrid(selected, texts, xCount, style ?? GUI.skin.button, options);
        }

        public static void SelectionGrid(ref int selected, string[] texts, int xCount, Action onChanged, GUIStyle style = null, params GUILayoutOption[] options)
        {
            int old = selected;
            SelectionGrid(ref selected, texts, xCount, style, options);
            if (selected != old)
            {
                onChanged?.Invoke();
            }
        }

    }
}
