using Harmony12;
using ModBase.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;

namespace ModBase
{
    public class Core<StorageType, SettingsType>
        where StorageType : class, new()
        where SettingsType : UnityModManager.ModSettings, new()
    {
        #region Fields & Properties

        private UnityModManager.ModEntry.ModLogger _logger;
        private Assembly _assembly;

        private event Action _onEnable;
        private event Action _onDisable;

        public StorageType Storage { get; private set; }

        public SettingsType Settings { get; private set; }

        public bool Enabled { get; private set; }

        public bool Patched { get; private set; }

        #endregion

        public Core(UnityModManager.ModEntry modEntry, Assembly assembly)
        {
            _logger = modEntry.Logger;
            _assembly = assembly;
        }

        #region Toggle

        public void Enable(UnityModManager.ModEntry modEntry)
        {
            DateTime startTime = DateTime.Now;
            Debug($"[{DateTime.Now - startTime:ss':'ff}] Enabling.");

            try
            {
                Debug($"[{DateTime.Now - startTime:ss':'ff}] Loading settings.");
                Settings = UnityModManager.ModSettings.Load<SettingsType>(modEntry);
                Storage = new StorageType();

                modEntry.OnSaveGUI += HandleOnSaveGUI;

                if (!Patched)
                {
                    HarmonyInstance harmonyInstance = HarmonyInstance.Create(modEntry.Info.Id);
                    foreach (Type type in _assembly.GetTypes())
                    {
                        List<HarmonyMethod> harmonyMethods = type.GetHarmonyMethods();
                        if (harmonyMethods != null && harmonyMethods.Count() > 0)
                        {
                            Debug($"[{DateTime.Now - startTime:ss':'ff}] Patching: {type.DeclaringType?.Name}.{type.Name}");
                            HarmonyMethod attributes = HarmonyMethod.Merge(harmonyMethods);
                            PatchProcessor patchProcessor = new PatchProcessor(harmonyInstance, type, attributes);
                            patchProcessor.Patch();
                        }
                    }
                }
                Patched = true;
            }
            catch (Exception e)
            {
                Disable(modEntry, true);
                throw e;
            }

            // register events
            Debug($"[{DateTime.Now - startTime:ss':'ff}] Registering events.");
            foreach (Type type in _assembly.GetTypes())
            {
                foreach (ModEvent modEvent in type.GetCustomAttributes<ModEvent>(true))
                {
                    switch (modEvent)
                    {
                        case ModEventOnEnable e:
                            _onEnable += Delegate.CreateDelegate(typeof(Action), type, e.HandlerName) as Action;
                            break;
                        case ModEventOnDisable e:
                            _onDisable += Delegate.CreateDelegate(typeof(Action), type, e.HandlerName) as Action;
                            break;
                    }
                }
            }

            Enabled = true;

            Debug($"[{DateTime.Now - startTime:ss':'ff}] Raising events: 'OnEnable'");
            _onEnable?.Invoke();

            Debug($"[{DateTime.Now - startTime:ss':'ff}] Enabled.");
        }

        public void Disable(UnityModManager.ModEntry modEntry, bool unpatch = false)
        {
            DateTime startTime = DateTime.Now;
            Debug($"[{DateTime.Now - startTime:ss':'ff}] Disabling.");

            // using try-catch to prevent the progression being disrupt by exceptions
            try
            {
                if (Enabled)
                {
                    Debug($"[{DateTime.Now - startTime:ss':'ff}] Raising events: 'OnDisable'");
                    _onDisable?.Invoke();
                }
            }
            catch (Exception e)
            {
                Error(e.ToString());
            }

            _onEnable = null;
            _onDisable = null;

            if (unpatch)
            {
                HarmonyInstance harmonyInstance = HarmonyInstance.Create(modEntry.Info.Id);
                foreach (MethodBase method in harmonyInstance.GetPatchedMethods().ToList())
                {
                    Patches patchInfo = harmonyInstance.GetPatchInfo(method);
                    List<Patch> patches = patchInfo.Transpilers.Concat(patchInfo.Postfixes).Concat(patchInfo.Prefixes)
                        .Where(patch => patch.owner == modEntry.Info.Id).ToList();
                    if (patches.Any())
                    {
                        Debug($"[{DateTime.Now - startTime:ss':'ff}] Unpatching: {patches.First().patch.DeclaringType.DeclaringType?.Name}.{method.DeclaringType.Name}.{method.Name}");
                        foreach (Patch patch in patches)
                        {
                            try
                            {
                                harmonyInstance.Unpatch(method, patch.patch);
                            }
                            catch (Exception e)
                            {
                                Error(e.ToString());
                            }
                        }
                    }
                }
                Patched = false;
            }

            modEntry.OnSaveGUI -= HandleOnSaveGUI;

            Storage = null;
            Settings = null;

            Enabled = false;

            Debug($"[{DateTime.Now - startTime:ss':'ff}] Disabled.");
        }

        #endregion

        #region Event Handlers

        private void HandleOnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            UnityModManager.ModSettings.Save(Settings, modEntry);
        }

        #endregion

        #region Loggers

        public void Critical(string str)
        {
            _logger.Critical(str);
        }

        public void Critical(object obj)
        {
            _logger.Critical(obj?.ToString() ?? "null");
        }

        public void Error(string str)
        {
            _logger.Error(str);
        }

        public void Error(object obj)
        {
            _logger.Error(obj?.ToString() ?? "null");
        }

        public void Log(string str)
        {
            _logger.Log(str);
        }

        public void Log(object obj)
        {
            _logger.Log(obj?.ToString() ?? "null");
        }

        public void Warning(string str)
        {
            _logger.Warning(str);
        }

        public void Warning(object obj)
        {
            _logger.Warning(obj?.ToString() ?? "null");
        }

        [Conditional("DEBUG")]
        public void Debug(string str)
        {
            _logger.Log(str);
        }

        [Conditional("DEBUG")]
        public void Debug(object obj)
        {
            _logger.Log(obj?.ToString() ?? "null");
        }

        #endregion
    }
}
