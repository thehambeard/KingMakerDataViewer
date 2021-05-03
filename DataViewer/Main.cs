using HarmonyLib;
using ModKit;
using ModKit.Utility;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker;

namespace DataViewer
{
#if (DEBUG)
    [EnableReloading]
#endif
    static class Main {
        public static ModManager<Core, Settings> Mod;
        public static Settings settings { get { return Mod.Settings; } }
        public static bool IsInGame { get { return Game.Instance.Player.Party.Any(); } }

        public static MenuManager Menu;
        public static UnityModManager.ModEntry modEntry = null;

        public static Rect ummRect = new Rect();
        public static float ummWidth = 960f;
        public static int ummTabID = 0;
        public static bool IsNarrow { get { return ummWidth < 1600; } }
        public static bool IsWide { get { return ummWidth >= 2000; } }

        public static void Log(string s) { if (modEntry != null) modEntry.Logger.Log(s); }
        public static void Log(int indent, string s) { Log("    ".Repeat(indent) + s); }
        static bool Load(UnityModManager.ModEntry modEntry) {
            Mod = new ModManager<Core, Settings>();
            Menu = new MenuManager();
            modEntry.OnToggle = OnToggle;
#if (DEBUG)
            modEntry.OnUnload = Unload;
            Main.modEntry = modEntry;
            return true;
        }

        static bool Unload(UnityModManager.ModEntry modEntry) {
            Mod.Disable(modEntry, true);
            Menu = null;
            Mod = null;
            return true;
        }
#else
            return true;
        }
#endif
        static void ModManagerPropertyChanged(object sender, PropertyChangedEventArgs e) {
            settings.selectedTab = Menu.tabIndex;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        { 
            if (value)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Mod.Enable(modEntry, assembly);
                Menu.Enable(modEntry, assembly);
                Menu.tabIndex = settings.selectedTab;
                Menu.PropertyChanged += ModManagerPropertyChanged;
            }
            else
            {
                Menu.Disable(modEntry);
                Mod.Disable(modEntry, false);
                ReflectionCache.Clear();
            }
            return true;
        }
    }
}
