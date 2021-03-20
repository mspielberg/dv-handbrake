using DV.Logic.Job;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HandBrake
{
    [EnableReloading]
    public static class Main
    {
        public static UnityModManager.ModEntry? mod;
        public static Settings settings = new Settings();

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            try
            {
                var loaded = Settings.Load<Settings>(modEntry);
                if (loaded.version == modEntry.Info.Version)
                {
                    settings = loaded;
                }
                else
                {
                    settings = new Settings() { version = mod.Info.Version };
                }
            }
            catch
            {
                settings = new Settings();
            }

            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;
            modEntry.OnToggle = OnToggle;

            return true;
        }

        static private void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }

        static private void OnSaveGui(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
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

        public static void DebugLog(Func<string> message)
        {
            if (settings.enableLogging)
                mod?.Logger.Log(message());
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

        [HarmonyPatch(typeof(TransportTask), nameof(TransportTask.UpdateTaskState))]
        public static class UpdateTaskStatePatch
        {
            public static bool IsFinalTask(TransportTask transportTask)
            {
                var job = transportTask.Job;
                IEnumerable<Task> tasks = job.tasks;
                while (true)
                {
                    var task = tasks.Last();
                    if (task is ParallelTasks parallel)
                        tasks = parallel.tasks;
                    else if (task is SequentialTasks sequential)
                        tasks = sequential.tasks;
                    else
                        return task == transportTask;
                }
            }

            public static void Postfix(TransportTask __instance, ref TaskState __result)
            {
                if (__instance.state == TaskState.Done && Main.settings.requireHandbrakeForTasks && IsFinalTask(__instance))
                {
                    foreach (Car car in __instance.cars)
                    {
                        var trainCar = SingletonBehaviour<IdGenerator>.Instance.logicCarToTrainCar[car];
                        if (trainCar.brakeSystem.independentBrakePosition < 0.5f)
                        {
                            __instance.SetState(TaskState.InProgress);
                            __result = __instance.state;
                            return;
                        }
                    }
                }
            }
        }
    }
}