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

    [HarmonyPatch(typeof(TrainCar), "OnEnable")]
    public static class HandBrake
    {
        public static void Postfix(TrainCar __instance)
        {
            if (__instance.brakeSystem.hasIndependentBrake)
                return;
            var cabooseController = __instance.gameObject.AddComponent<CabooseController>();
            cabooseController.cabTeleportDestinationCollidersGO = new GameObject();
            if (UnityModManager.FindMod("AirBrake") != null)
                __instance.brakeSystem.independentBrakePosition = 1f;
        }
    }

    [HarmonyPatch(typeof(CabooseController), "Start")]
    public static class CabooseControllerStartPatch
    {
        public static void Postfix(CabooseController __instance)
        {
            if (__instance.GetComponent<TrainCar>().carType != TrainCarType.CabooseRed)
                __instance.GetComponent<CarDamageModel>().IgnoreDamage(false);
        }
    }
}