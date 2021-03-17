using HarmonyLib;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HandBrake
{
    [EnableReloading]
    public static class Main
    {
        public static UnityModManager.ModEntry? mod;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            modEntry.OnToggle = OnToggle;
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            if (value)
                harmony.PatchAll();
            else
                harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }

        public static void DebugLog(string message)
        {
            mod?.Logger.Log(message);
        }
    }

    public static class HandBrake
    {
        [HarmonyPatch(typeof(TrainCar), "OnEnable")]
        public static class TrainCarOnEnablePatch
        {
            public static void Postfix(TrainCar __instance)
            {
                if (CarTypes.IsAnyLocomotiveOrTender(__instance.carType))
                    return;
                if (__instance.GetComponent<CabooseController>() == null)
                {
                    var cabooseController = __instance.gameObject.AddComponent<CabooseController>();
                    cabooseController.cabTeleportDestinationCollidersGO = new GameObject();
                }
                if (UnityModManager.FindMod("AirBrake")?.Enabled ?? false)
                    __instance.StartCoroutine(DelayedSetIndependent(__instance));
            }

            private static IEnumerator DelayedSetIndependent(TrainCar car)
            {
                yield return null;
                int lastTrainsetSize = car.trainset.cars.Count;
                // Wait for auto coupling to finish
                while (true)
                {
                    yield return WaitFor.SecondsRealtime(1.0f);
                    // Main.DebugLog($"{Time.time} Checking {car.ID}: {car.trainset.cars.Count} cars in trainset: {string.Join(",",car.trainset.cars.Select(car=>car.ID))}");
                    if (car.trainset.cars.Count == lastTrainsetSize)
                        break;
                    else
                        lastTrainsetSize = car.trainset.cars.Count;
                }
                // Main.DebugLog($"No change in trainset size. loco count={car.trainset.locoIndices.Count}");
                if (car.trainset.locoIndices.Count == 0)
                {
                    car.GetComponent<CabooseController>().SetIndependentBrake(1f);
                }
            }

        }

        [HarmonyPatch(typeof(CabooseController), "Start")]
        public static class CabooseControllerStartPatch
        {
            public static void Postfix(CabooseController __instance)
            {
                if (!CarTypes.IsCaboose(__instance.car.carType))
                    __instance.GetComponent<CarDamageModel>().IgnoreDamage(false);
            }
        }

        [HarmonyPatch(typeof(CarKeyboardInputCaboose), nameof(CarKeyboardInputCaboose.CheckIndependentBreakInput))]
        public static class CheckIndependentBreakInputPatch
        {
            private static void SetAllIndependentBrakes(float value)
            {
                foreach (var car in PlayerManager.Car.trainset.cars)
                {
                    CabooseController? controller = car.GetComponent<CabooseController>();
                    controller?.SetIndependentBrake(value);
                }
            }

            public static void Postfix()
            {
                if (!PlayerManager.Car)
                    return;
                if (KeyCode.LeftShift.IsPressed() || KeyCode.RightShift.IsPressed())
                {
                    if (KeyBindings.increaseIndependentBrakeKeys.IsPressed())
                        SetAllIndependentBrakes(1f);
                    else if (KeyBindings.decreaseIndependentBrakeKeys.IsPressed())
                        SetAllIndependentBrakes(0f);
                }
            }
        }
    }
}