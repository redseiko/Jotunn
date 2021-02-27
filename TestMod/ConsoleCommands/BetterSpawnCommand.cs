﻿using UnityEngine;
using ValheimLokiLoader;
using ValheimLokiLoader.Managers;

namespace TestMod.ConsoleCommands
{
    public class BetterSpawnCommand : ConsoleCommand
    {
        public override string Name => "better_spawn";

        public override string Help => "like spawn but BETTER";

        public override void Run(string[] args)
        {
            GameObject prefab = PrefabManager.GetPrefab(args[0]);

            if (!prefab)
            {
                Console.instance.Print("that doesn't exist: " + args[0]);
                return;
            }

            UnityEngine.Object.Instantiate<GameObject>(prefab, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 2f + Vector3.up, Quaternion.identity).GetComponent<Character>();
        }
    }
}
