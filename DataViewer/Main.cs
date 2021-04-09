using ModMaker;
using ModMaker.Utility;
using System.Reflection;
using UnityModManagerNet;

namespace DataViewer
{
#if (DEBUG)
    [EnableReloading]
#endif
    static class Main
    {
        public static ModManager<Core, Settings> Mod;
        public static Settings settings { get { return Mod.Settings; } }

        public static MenuManager Menu;
        public static UnityModManager.ModEntry modEntry = null;
        public static void Log(string s) { if (modEntry != null) modEntry.Logger.Log(s); }
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = new ModManager<Core, Settings>();
            Menu = new MenuManager();
            modEntry.OnToggle = OnToggle;
#if (DEBUG)
            modEntry.OnUnload = Unload;
            Main.modEntry = modEntry;
            return true;
        }

        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            Mod.Disable(modEntry, true);
            Menu = null;
            Mod = null;
            return true;
        }
#else
            return true;
        }
#endif

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        { 
            if (value)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Mod.Enable(modEntry, assembly);
                Menu.Enable(modEntry, assembly);
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
