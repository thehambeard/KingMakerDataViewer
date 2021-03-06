﻿using DataViewer.Utility;
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
        private static readonly Dictionary<string, Func<object>> TARGET_LIST = new Dictionary<string, Func<object>>()
        {
            { "None", null },
            { "Game", () => Game.Instance },
            { "Units", () => Game.Instance?.State?.Units },
            { "Dialog", () => Game.Instance?.DialogController },
            { "Vendor", () => Game.Instance?.Vendor },
            { "Scene", () => SceneManager.GetActiveScene() },
            { "Area", () => Game.Instance?.CurrentScene },
            { "UI", () => Game.Instance?.UI },
            { "Static Canvas", () => Game.Instance?.UI?.Canvas?.gameObject },
            { "Global Map", () => Game.Instance?.Player?.GlobalMap },
            { "Player", () => Game.Instance?.Player },
            { "Characters", () => Game.Instance?.Player?.AllCharacters },
            { "Inventory", () => Game.Instance?.Player?.Inventory },
            { "Quest Book", () => Game.Instance?.Player?.QuestBook },
            { "Kingdom", () => Game.Instance?.Player?.Kingdom },
       };

        private readonly string[] _targetNames = TARGET_LIST.Keys.ToArray();

        private ReflectionTreeView _treeView = new ReflectionTreeView();
        private int _targetIndex;

        public string Name => "Raw Data";

        public int Priority => 0;

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Mod == null || !Mod.Enabled)
                return;

            try
            {
                // target selection
                GUIHelper.SelectionGrid(ref _targetIndex, _targetNames, 5, () =>
                {
                    Func<object> getTarget = TARGET_LIST[_targetNames[_targetIndex]];
                    if (getTarget == null)
                        _treeView.Clear();
                    else
                        _treeView.SetRoot(getTarget());
                });

                // tree view
                if (_targetIndex != 0)
                {
                    GUILayout.Space(10f);

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
