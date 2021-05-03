using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using ModKit;
using ModKit.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;
using static DataViewer.Main;

namespace DataViewer.Menus {
    public class FeaturesTreeViewer : IMenuSelectablePage {
        private UnitEntityData _selectedCharacter = null;
        private FeaturesTree _featuresTree;

        private GUIStyle _buttonStyle;

        public string Name => "Features Tree";

        public int Priority => 500;

        public void OnGUI(UnityModManager.ModEntry modEntry) {
            if (Mod == null || !Mod.Enabled)
                return;

            string activeScene = SceneManager.GetActiveScene().name;
            if (Game.Instance?.Player == null || activeScene == "MainMenu" || activeScene == "Start") {
                GUILayout.Label(" * Please start or load the game first.".color(RGBA.yellow));
                return;
            }
            if (_buttonStyle == null)
                _buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true };

            try {

                using (new GUILayout.HorizontalScope()) {

                    // character selection
                    using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false))) {
                        List<UnitEntityData> companions = Game.Instance?.Player.AllCharacters
                                .Where(c => c.IsPlayerFaction).ToList(); // && !c.Descriptor.IsPet).ToList();

                        int selectedCharacterIndex = companions.IndexOf(_selectedCharacter) + 1;
                        GUIHelper.SelectionGrid(ref selectedCharacterIndex,
                            new string[] { "None" }.Concat(companions.Select(item => item.CharacterName)).ToArray(),
                            1, () => {
                                if (selectedCharacterIndex > 0) {
                                    _selectedCharacter = companions[selectedCharacterIndex - 1];
                                    modEntry.Logger.Log($"selected: {_selectedCharacter.CharacterName}");
                                    _featuresTree = new FeaturesTree(_selectedCharacter.Descriptor.Progression);
                                }
                            }, null, GUILayout.ExpandWidth(false));
                        if (selectedCharacterIndex == 0 &&
                            (Event.current.type == EventType.Layout || Event.current.type == EventType.Used)) {
                            _selectedCharacter = null;
                            _featuresTree = null;
                        }
                    }

                    // features tree
                    if (_featuresTree != null)
                        using (new GUILayout.VerticalScope()) {
                            bool expandAll;
                            bool collapseAll;

                            // draw tool bar
                            using (new GUILayout.HorizontalScope()) {
                                if (GUILayout.Button("Refresh")) {
                                    _featuresTree = new FeaturesTree(_selectedCharacter.Descriptor.Progression);
                                }
                                expandAll = GUILayout.Button("Expand All");
                                collapseAll = GUILayout.Button("Collapse All");
                            }

                            GUILayout.Space(10f);

                            // draw tree
                            foreach (FeaturesTree.FeatureNode node in _featuresTree.RootNodes) {
                                draw(node);
                            }

                            void draw(FeaturesTree.FeatureNode node) {
                                using (new GUILayout.HorizontalScope()) {
                                    var titleText = node.Name.Bold() + ("\n      [" + node.Blueprint.name + "]").color(node.IsMissing ? RGBA.maroon : RGBA.aqua);
                                    if (node.ChildNodes.Count > 0) {
                                        if (node.Expanded == ToggleState.None) {
                                            node.Expanded = ToggleState.Off;
                                        }
                                        node.Expanded = (expandAll ? ToggleState.On : collapseAll ? ToggleState.Off : node.Expanded);
                                    }
                                    else {
                                        node.Expanded = ToggleState.None;
                                    }
                                    Main.Log($"{node.Expanded} {titleText}");
                                    UI.ToggleButton(ref node.Expanded, titleText, _buttonStyle);
                                    if (node.Expanded.IsOn()) {
                                        using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false))) {
                                            foreach (var child in node.ChildNodes)
                                                draw(child);
                                        }
                                    }
                                    else {
                                        GUILayout.FlexibleSpace();
                                    }
                                }
                            }
                        }
                }
            }
            catch (Exception e) {
                _selectedCharacter = null;
                _featuresTree = null;
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }
        }

        private class FeaturesTree {
            public readonly List<FeatureNode> RootNodes = new List<FeatureNode>();

            public FeaturesTree(UnitProgressionData progression) {
                Dictionary<BlueprintScriptableObject, FeatureNode> normalNodes = new Dictionary<BlueprintScriptableObject, FeatureNode>();
                List<FeatureNode> parametrizedNodes = new List<FeatureNode>();

                //Main.Log($"prog: {progression}");
                // get nodes (features / race)
                foreach (var feature in progression.Features.Enumerable) {
                    var name = feature.Name;
                    if (name == null || name.Length == 0)
                        name = feature.Blueprint.name;
                    //Main.Log($"feature: {name}");
                    var source = feature.m_Source;
                    //Main.Log($"source: {source}");
                    if (feature.Blueprint is BlueprintParametrizedFeature)
                        parametrizedNodes.Add(new FeatureNode(name, feature.Blueprint, source));
                    else
                        normalNodes.Add(feature.Blueprint, new FeatureNode(name, feature.Blueprint, source));
                }

                // get nodes (classes)
                foreach (BlueprintCharacterClass characterClass in progression.Classes.Select(item => item.CharacterClass)) {
                    normalNodes.Add(characterClass, new FeatureNode(characterClass.Name, characterClass, null));
                }

                // set source selection
                List<FeatureNode> selectionNodes = normalNodes.Values
                    .Where(item => item.Blueprint is BlueprintFeatureSelection).ToList();
                for (int i = 0; i <= 20; i++) {
                    foreach (var selection in selectionNodes) {
                        foreach (BlueprintFeature feature in progression.GetSelections(selection.Blueprint as BlueprintFeatureSelection, i)) {
                            FeatureNode node = default;
                            if (feature is BlueprintParametrizedFeature) {
                                node = parametrizedNodes
                                    .FirstOrDefault(item => item.Source != null && item.Source == selection.Source);
                            }

                            if (node != null || normalNodes.TryGetValue(feature, out node)) {
                                node.Source = selection.Blueprint;
                            }
                            else {
                                // missing child
                                normalNodes.Add(feature,
                                    new FeatureNode(string.Empty, feature, selection.Blueprint) { IsMissing = true });
                            }
                        }
                    }
                }

                // build tree
                foreach (FeatureNode node in normalNodes.Values.Concat(parametrizedNodes).ToList()) {
                    if (node.Source == null) {
                        RootNodes.Add(node);
                    }
                    else if (normalNodes.TryGetValue(node.Source, out FeatureNode parent)) {
                        parent.ChildNodes.Add(node);
                    }
                    else {
                        // missing parent
                        parent = new FeatureNode(string.Empty, node.Source, null) { IsMissing = true };
                        parent.ChildNodes.Add(node);
                        normalNodes.Add(parent.Blueprint, parent);
                        RootNodes.Add(parent);
                    }
                }
            }

            public class FeatureNode {
                internal bool IsMissing;
                internal BlueprintScriptableObject Source;

                public readonly string Name;
                public readonly BlueprintScriptableObject Blueprint;
                public readonly List<FeatureNode> ChildNodes = new List<FeatureNode>();

                public ToggleState Expanded;

                internal FeatureNode(string name, BlueprintScriptableObject blueprint, BlueprintScriptableObject source) {
                    Name = name;
                    Blueprint = blueprint;
                    Source = source;
                }
            }
        }
    }
}
