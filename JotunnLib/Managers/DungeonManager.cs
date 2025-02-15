using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers.MockSystem;
using Jotunn.Utils;
using UnityEngine;

namespace Jotunn.Managers
{
    /// <summary>
    ///    Manager for handling all custom data added to the game related to creatures.
    /// </summary>
    public class DungeonManager : IManager
    {
        private static DungeonManager _instance;

        /// <summary>
        ///     The singleton instance of this manager.
        /// </summary>
        public static DungeonManager Instance => _instance ??= new DungeonManager();

        /// <summary>
        ///     Event that gets fired after the vanilla <see cref="DungeonDB"/> has loaded rooms.  Your code will execute
        ///     every time a main scene is started (on joining a game). <br />
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnVanillaRoomsAvailable;

        /// <summary>
        ///     Event that gets fired after all custom rooms are registered to the <see cref="DungeonDB"/>.  Your code will execute
        ///     every time a main scene is started (on joining a game). <br />
        ///     If you want to execute just once you will need to unregister from the event after execution.
        /// </summary>
        public static event Action OnRoomsRegistered;

        /// <summary>
        ///     Internal dictionary of all custom rooms
        /// </summary>
        internal readonly Dictionary<string, CustomRoom> Rooms = new Dictionary<string, CustomRoom>();

        private readonly Dictionary<int, string> hashToName = new Dictionary<int, string>();
        private readonly List<string> themeList = new List<string>();
        private readonly Dictionary<string, GameObject> loadedEnvironments = new Dictionary<string, GameObject>();

        /// <summary>
        ///     Container for Jötunn's DungeonRooms in the DontDestroyOnLoad scene.
        /// </summary>
        private GameObject DungeonRoomContainer;

        /// <summary>
        ///     Hide .ctor
        /// </summary>
        private DungeonManager() { }

        static DungeonManager()
        {
            ((IManager)Instance).Init();
        }

        /// <summary>
        ///     Creates the spawner container and registers all hooks.
        /// </summary>
        void IManager.Init()
        {
            Main.LogInit("DungeonManager");

            DungeonRoomContainer = new GameObject("DungeonRooms");
            DungeonRoomContainer.transform.parent = Main.RootObject.transform;
            DungeonRoomContainer.SetActive(false);

            Main.Harmony.PatchAll(typeof(Patches));
        }

        private static class Patches
        {
            [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.SetupLocations)), HarmonyPrefix]
            private static void OnBeforeZoneSystemSetupLocations() => Instance.OnZoneSystemSetupLocations();

            [HarmonyPatch(typeof(DungeonDB), nameof(DungeonDB.Start)), HarmonyPostfix]
            private static void OnDungeonDBStarted() => Instance.OnDungeonDBStarted();

            [HarmonyPatch(typeof(DungeonDB), nameof(DungeonDB.GetRoom)), HarmonyPrefix]
            private static bool OnDungeonDBGetRoom(int hash, ref DungeonDB.RoomData __result)
            {
                DungeonDB.RoomData result = Instance.OnDungeonDBGetRoom(hash);

                if (result != null)
                {
                    __result = result;
                    return false;
                }

                return true;
            }

            [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.SetupAvailableRooms)), HarmonyPostfix]
            private static void OnDungeonGeneratorSetupAvailableRooms(DungeonGenerator __instance) => Instance.OnDungeonGeneratorSetupAvailableRooms(__instance);
        }

        /// <summary>
        ///     Add a <see cref="CustomRoom"/> to the game.<br />
        ///     Checks if the custom room is valid and unique and adds it to the list of custom rooms.
        /// </summary>
        /// <param name="customRoom">The custom Room to add.</param>
        /// <returns>true if the custom Room was added to the manager.</returns>
        public bool AddCustomRoom(CustomRoom customRoom)
        {
            if (customRoom == null)
            {
                throw new ArgumentException("Cannot be null", nameof(customRoom));
            }

            if (string.IsNullOrEmpty(customRoom.ThemeName))
            {
                throw new ArgumentException($"ThemeName of this room must have a value.", nameof(customRoom));
            }

            if (!CustomRoom.IsVanillaTheme(customRoom.ThemeName) && !themeList.Contains(customRoom.ThemeName))
            {
                throw new ArgumentException($"ThemeName of this room ({customRoom.ThemeName}) match a vanilla Room.Theme value or must be registered.", nameof(customRoom));
            }

            if (Rooms.ContainsKey(customRoom.Name))
            {
                Logger.LogWarning(customRoom.SourceMod, $"Room {customRoom.Name} already exists");
                return false;
            }

            customRoom.Prefab.transform.SetParent(DungeonRoomContainer.transform);
            customRoom.Prefab.SetActive(true);
            Rooms.Add(customRoom.Name, customRoom);
            return true;
        }

        /// <summary>
        ///     Get a custom room by its name.
        /// </summary>
        /// <param name="name">Name of the custom room to search.</param>
        /// <returns>The <see cref="CustomRoom"/> if found.</returns>
        public CustomRoom GetRoom(string name)
        {
            return Rooms.TryGetValue(name, out var room) ? room : null;
        }

        /// <summary>
        ///     Remove a custom room by its name.
        /// </summary>
        /// <param name="name">Name of the room to remove.</param>
        public bool RemoveRoom(string name)
        {
            return Rooms.Remove(name);
        }

        /// <summary>
        ///     Registers a new dungeon theme, identified by a unique theme name string.<br />
        ///     Assets can be added at any time and will be registered as soon as the vanilla loader is ready.
        /// </summary>
        /// <param name="prefab">The <see cref="DungeonGenerator"/> prefab.</param>
        /// <param name="themeName">The name of the theme to register.</param>
        /// <returns>True if theme is successfullly registered, otherwise false.</returns>
        public bool RegisterDungeonTheme(GameObject prefab, string themeName)
        {
            Logger.LogDebug($"RegisterDungeonTheme called with prefab {prefab.name} and themeName {themeName}.");

            if (prefab == null)
            {
                throw new ArgumentException("Cannot be null", nameof(prefab));
            }

            if (string.IsNullOrEmpty(themeName))
            {
                throw new ArgumentException("Cannot be null or empty", nameof(themeName));
            }

            DungeonGenerator dg = prefab.GetComponentInChildren<DungeonGenerator>();
            if (dg == null)
            {
                Logger.LogError($"Cannot find DungeonGenerator component in prefab {prefab.name}.");
                throw new ArgumentException("Prefab must contain a DungeonGenerator component", nameof(prefab));
            }

            DungeonGeneratorTheme dgt;
            if (dg.gameObject.TryGetComponent(out dgt))
            {
                if (!string.IsNullOrEmpty(dgt.m_themeName) && dgt.m_themeName != themeName)
                {
                    Logger.LogWarning($"Overwriting existing theme name {dgt.m_themeName} with {themeName}.");
                }
            }
            else
            {
                dgt = dg.gameObject.AddComponent<DungeonGeneratorTheme>();
            }

            dgt.m_themeName = themeName;
            themeList.Add(themeName);

            return true;
        }


        /// <summary>
        ///     Adds a new environment prefab to be registered when <see cref="ZoneSystem.SetupLocations"/> runs.<br />
        ///     If you intend to use a custom interior environment <see cref="Location.m_interiorEnvironment"/>, this method enables you 
        ///     to provide a prefab with an appropriately configured <see cref="LocationList"/> containing atleast one
        ///     <see cref="EnvSetup"/> within <see cref="LocationList.m_environments"/>.
        /// </summary>
        /// <param name="assetBundle">The <see cref="AssetBundle"/> containing the prefab.</param>
        /// <param name="prefabName">The name of the prefab to register.</param>
        public void RegisterEnvironment(AssetBundle assetBundle, string prefabName)
        {
            if (assetBundle == null)
            {
                throw new ArgumentException("Cannot be null", nameof(assetBundle));
            }

            if (string.IsNullOrEmpty(prefabName))
            {
                throw new ArgumentException("Cannot be null or empty", nameof(prefabName));
            }

            var env = assetBundle.LoadAsset<GameObject>(prefabName);
            loadedEnvironments.Add(prefabName, env);
        }

        private void GenerateHashes()
        {
            hashToName.Clear();

            foreach (CustomRoom room in Rooms.Values)
            {
                int stableHashCode = room.Prefab.name.GetStableHashCode();
                if (hashToName.ContainsKey(stableHashCode))
                {
                    Logger.LogWarning($"Room {room.Name} is already registered with hash {stableHashCode}");
                }
                else
                {
                    hashToName.Add(stableHashCode, room.Name);
                }
            }
        }

        private void OnZoneSystemSetupLocations()
        {
            if (loadedEnvironments.Count > 0)
            {
                foreach (var kvp in loadedEnvironments)
                {
                    Logger.LogDebug($"Registering environment {kvp.Key}.");

                    var env = kvp.Value;
                    env.FixReferences(true);
                    UnityEngine.Object.Instantiate<GameObject>(env); // Intentionally omitting DungeonRoomContainer.transform
                }
            }
        }

        private void OnDungeonDBStarted()
        {
            InvokeOnVanillaRoomsAvailable();

            if (Rooms.Count > 0)
            {
                hashToName.Clear();

                List<string> toDelete = new List<string>();
                Logger.LogInfo($"Registering {Rooms.Count} custom rooms");
                foreach (CustomRoom customRoom in Rooms.Values)
                {
                    try
                    {
                        Logger.LogDebug($"Adding custom room {customRoom.Name} with {customRoom.ThemeName} theme");
                        if (customRoom.FixReference)
                        {
                            customRoom.Prefab.FixReferences(true);
                            customRoom.FixReference = false;
                        }

                        if (CustomRoom.IsVanillaTheme(customRoom.ThemeName))
                        {
                            RegisterRoomInDungeonDB(customRoom);
                        }
                    }
                    catch (MockResolveException ex)
                    {
                        Logger.LogWarning(customRoom.SourceMod, $"Skipping Room {customRoom}: could not resolve mock {ex.MockType.Name} {ex.FailedMockName}");
                        toDelete.Add(customRoom.Name);
                    }
                    catch (Exception ex2)
                    {
                        Logger.LogWarning(customRoom.SourceMod, $"Exception caught while adding Room: {ex2}");
                        toDelete.Add(customRoom.Name);
                    }
                }

                foreach (string name in toDelete)
                {
                    Rooms.Remove(name);
                }

                DungeonDB.instance.GenerateHashList();
                GenerateHashes();
            }

            InvokeOnRoomsRegistered();
        }

        private void OnDungeonGeneratorSetupAvailableRooms(DungeonGenerator self)
        {
            DungeonGeneratorTheme proxy = self.gameObject.GetComponent<DungeonGeneratorTheme>();

            if (DungeonGenerator.m_availableRooms != null)
            {
                if (proxy != null)
                {
                    Logger.LogDebug($"Found DungeonGeneratorTheme component in prefab with name {self.gameObject}");
                    Logger.LogDebug($"This dungeon generator has a custom theme = {proxy.m_themeName}, adding available rooms");

                    var selectedRooms = Rooms.Values
                        .Where(r => r.Room.m_enabled)
                        .Where(r => r.ThemeName == proxy.m_themeName);

                    foreach (var room in selectedRooms)
                    {
                        Logger.LogDebug($"Adding Room with name {room.Name} and theme {room.ThemeName}");
                        DungeonGenerator.m_availableRooms.Add(room.RoomData);
                    }
                }
                else if (self.m_themes != Room.Theme.None)
                {
                    Logger.LogDebug($"No DungeonGeneratorTheme component in prefab with name {self.gameObject}");
                    Logger.LogDebug($"Adding additional rooms of type {self.m_themes} to available rooms");

                    var selectedRooms = Rooms.Values
                        .Where(r => r.Room.m_enabled)
                        .Where(r => Enum.TryParse(r.ThemeName, false, out Room.Theme theme) ? theme != Room.Theme.None && self.m_themes.HasFlag(theme) : false);

                    foreach (var room in selectedRooms)
                    {
                        Logger.LogDebug($"Adding Room with name {room.Name} and theme {room.ThemeName}");
                        DungeonGenerator.m_availableRooms.Add(room.RoomData);
                    }
                }
                else
                {
                    Logger.LogWarning($"DungeonManager's SetupAvailableRooms was invoked without a valid DungeonGeneratorTheme or DungeonGenerator.m_themes value. Something may be wrong with {self.name}'s generator.");
                }

                if (DungeonGenerator.m_availableRooms.Count <= 0)
                {
                    Logger.LogDebug($"DungeonManager's SetupAvailableRooms yielded zero rooms.");
                }
            }
        }

        private void InvokeOnVanillaRoomsAvailable()
        {
            OnVanillaRoomsAvailable?.SafeInvoke();
        }

        private void InvokeOnRoomsRegistered()
        {
            OnRoomsRegistered?.SafeInvoke();
        }

        private void RegisterRoomInDungeonDB(CustomRoom room)
        {
            DungeonDB.instance.m_rooms.Add(room.RoomData);
        }

        /// <summary>
        ///     Attempt to get room by hash.
        /// </summary>
        /// <param name="hash"></param>
        private DungeonDB.RoomData OnDungeonDBGetRoom(int hash)
        {
            if (hashToName.TryGetValue(hash, out var roomName) && Rooms.TryGetValue(roomName, out var room))
            {
                return room.RoomData;
            }

            return null;
        }
    }
}
