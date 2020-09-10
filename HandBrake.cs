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

    [HarmonyPatch(typeof(Brakeset), nameof(Brakeset.Update))]
    [HarmonyAfter(new string[] { "AirBrake" })]
    static public class HandBrake
    {
        const float Increment = 0.05f;

        static void Postfix()
        {
            if (!Main.enabled)
                return;

            var car = PlayerManager.Car;
            if (car == null || car.brakeSystem.hasIndependentBrake)
                return;

            var adjust =
                KeyBindings.increaseIndependentBrakeKeys.IsPressed() ? Increment
                : KeyBindings.decreaseIndependentBrakeKeys.IsPressed() ? -Increment
                : 0f;

            car.brakeSystem.independentBrakePosition = Mathf.Clamp01(car.brakeSystem.independentBrakePosition + adjust);
            car.brakeSystem.brakingFactor = Mathf.Max(car.brakeSystem.brakingFactor, car.brakeSystem.independentBrakePosition);
        }
    }
}