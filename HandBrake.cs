using HarmonyLib;
using System.Collections;
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
                var cabooseController = __instance.gameObject.AddComponent<CabooseController>();
                cabooseController.cabTeleportDestinationCollidersGO = new GameObject();
            }
        }

        [HarmonyPatch(typeof(CabooseController), "Start")]
        public static class CabooseControllerStartPatch
        {
            private static IEnumerator DelayedSetIndependent(CabooseController controller)
            {
                TrainCar car = controller.car;
                int lastTrainsetSize = car.trainset.cars.Count;
                // Wait for auto coupling to finish
                while (true)
                {
                    yield return WaitFor.SecondsRealtime(1.0f);
                    if (car.trainset.cars.Count == lastTrainsetSize)
                        break;
                    else
                        lastTrainsetSize = car.trainset.cars.Count;
                }
                if (car.trainset.locoIndices.Count == 0)
                    car.brakeSystem.independentBrakePosition = controller.targetIndependentBrake = 1f;
            }

            public static void Postfix(CabooseController __instance)
            {
                if (!CarTypes.IsCaboose(__instance.car.carType))
                    __instance.GetComponent<CarDamageModel>().IgnoreDamage(false);
                if (UnityModManager.FindMod("AirBrake") != null)
                    __instance.StartCoroutine(DelayedSetIndependent(__instance));
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