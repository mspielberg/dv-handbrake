using DV.Simulation.Brake;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HandBrake
{
    [EnableReloading]
    public static class Main
    {
        public static UnityModManager.ModEntry? mod;
        public static bool enabled;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value != enabled)
            {
                enabled = value;
            }
            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }
    }

    [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.OnEnable))]
    static class HandBrake
    {
        static void Postfix(TrainCar __instance)
        {
            if (__instance.brakeSystem.hasIndependentBrake)
                return;
            var cabooseController = __instance.gameObject.AddComponent<CabooseController>();
            cabooseController.cabTeleportDestinationCollidersGO = new GameObject();
        }
    }

    [HarmonyPatch(typeof(CabooseController), nameof(CabooseController.Start))]
    static class CabooseControllerStartPatch
    {
        static void Postfix(CabooseController __instance)
        {
            if (__instance.GetComponent<TrainCar>().carType != TrainCarType.CabooseRed)
                __instance.GetComponent<CarDamageModel>().IgnoreDamage(false);
        }
    }
}