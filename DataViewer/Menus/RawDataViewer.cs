using DataViewer.Utility;
using Kingmaker;
using ModMaker;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;
using static DataViewer.Main;

namespace DataViewer.Menus
{
    public class RawDataViewer : IMenuSelectablePage
    {
        public static IEnumerable<Scene> GetAllScenes() {
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                yield return SceneManager.GetSceneAt(i);
            }
        }
        private static readonly Dictionary<string, Func<object>> TARGET_LIST = new Dictionary<string, Func<object>>()
        {
            { "None", null },
            { "Game", () => Game.Instance },
            { "Units", () => Game.Instance?.State?.Units },
            { "Dialog", () => Game.Instance?.DialogController },
            { "Vendor", () => Game.Instance?.Vendor },
            { "Scene", () => SceneManager.GetActiveScene() },
            { "Area", () => Game.Instance?.CurrentlyLoadedArea },
            { "UI", () => Game.Instance?.UI },
            { "Static Canvas", () => Game.Instance?.UI?.Canvas?.gameObject },
            { "Global Map", () => Game.Instance?.Player?.GlobalMap },
            { "Player", () => Game.Instance?.Player },
            { "Characters", () => Game.Instance?.Player?.AllCharacters },
            { "Inventory", () => Game.Instance?.Player?.Inventory },
            { "Quest Book", () => Game.Instance?.Player?.QuestBook },
            { "Kingdom", () => Game.Instance?.Player?.Kingdom },
            { "Root Game Objects", () => RawDataViewer.GetAllScenes().SelectMany(s => s.GetRootGameObjects()) },
            { "Game Objects", () => UnityEngine.Object.FindObjectsOfType<GameObject>() },
            { "Unity Resources", () =>  Resources.FindObjectsOfTypeAll(typeof(GameObject)) },
       };

        private readonly string[] _targetNames = TARGET_LIST.Keys.ToArray();

        private ReflectionTreeView _treeView = null;
       
        public string Name => "Raw Data";

        public int Priority => 0;
        void ResetTree() {
            if (_treeView == null)
                _treeView = new ReflectionTreeView();

            Func<object> getTarget = TARGET_LIST[_targetNames[Main.settings.selectedRawDataType]];
            if (getTarget == null)
                _treeView.Clear();
            else
                _treeView.SetRoot(getTarget());
        }
        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Mod == null || !Mod.Enabled)
                return;

            try
            {
                if (_treeView == null)
                    ResetTree();

                // target selection
                GUIHelper.SelectionGrid(ref Main.settings.selectedRawDataType, _targetNames, 5, () => {
                    ResetTree();
                });

                // tree view
                if (Main.settings.selectedRawDataType != 0)
                {
                    GUILayout.Space(10f);

                    _treeView.OnGUI();
                }
            }
            catch (Exception e)
            {
                Main.settings.selectedRawDataType = 0;
                _treeView.Clear();
                modEntry.Logger.Error(e.StackTrace);
                throw e;
            }
        }

    }
}
