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
                    MessageBox.ShowStartupMessage("Welcome to the Hand Brake mod.\nSpawned cars come with some brakes set and you must set them to turn in jobs. You can change how many are required in the Control-F10 menu.");
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
            {
                harmony.PatchAll();
                CarSpawner.CarSpawned += HandBrake.OnCarSpawned;
            }
            else
            {
                CarSpawner.CarSpawned -= HandBrake.OnCarSpawned;
                harmony.UnpatchAll(modEntry.Info.Id);
            }
            return true;
        }

        public static void DebugLog(Func<string> message)
        {
            if (settings.enableLogging)
                mod?.Logger.Log(message());
        }
    }

    public static class MessageBox
    {
        public static void ShowStartupMessage(string message)
        {
            if (WorldStreamingInit.isLoaded)
            {
                StartCoro(message);
            }
            else
            {
                WorldStreamingInit.LoadingFinished += () => StartCoro(message);
            }
        }

        private static void StartCoro(string message)
        {
            SingletonBehaviour<CanvasSpawner>.Instance.StartCoroutine(Coro(message));
        }

        private static IEnumerator Coro(string message)
        {
            while (!SingletonBehaviour<CanvasSpawner>.Instance.MenuLoaded)
                yield return null;
            while (DV.AppUtil.IsPaused)
                yield return null;
            while (SingletonBehaviour<CanvasSpawner>.Instance.IsOpen)
                yield return null;
            yield return WaitFor.Seconds(1f);
            MenuScreen? menuScreen = SingletonBehaviour<CanvasSpawner>.Instance.CanvasGO.transform.Find("TutorialPrompt")?.GetComponent<MenuScreen>();
            TutorialPrompt? tutorialPrompt = menuScreen?.GetComponentInChildren<TutorialPrompt>(includeInactive: true);
            if (menuScreen != null && tutorialPrompt != null)
            {
                tutorialPrompt.SetText(message);
                SingletonBehaviour<CanvasSpawner>.Instance.Open(menuScreen, pauseGame: false);
            }
        }
    }

    public static class HandBrake
    {
        public static void OnCarSpawned(TrainCar car)
        {
            if (CarTypes.IsAnyLocomotiveOrTender(car.carType))
                return;
            if (car.GetComponent<CabooseController>() == null)
            {
                var cabooseController = car.gameObject.AddComponent<CabooseController>();
                cabooseController.cabTeleportDestinationCollidersGO = new GameObject();
            }
            if (UnityModManager.FindMod("AirBrake")?.Enabled ?? false)
                car.StartCoroutine(DelayedSetIndependent(car));
        }

        private static IEnumerator DelayedSetIndependent(TrainCar car)
        {
            yield return null;
            // Wait for auto coupling to finish
            int lastTrainsetSize;
            do
            {
                lastTrainsetSize = car.trainset.cars.Count;
                yield return WaitFor.SecondsRealtime(1.0f);
                if (car.indexInTrainset > 0)
                {
                    Main.DebugLog(() => $"{car.ID} no longer front of trainset");
                    yield break;
                }
            }
            while (car.trainset.cars.Count != lastTrainsetSize);

            if (car.trainset.locoIndices.Count > 0)
            {
                Main.DebugLog(() => $"{car.ID} has loco in trainset");
                yield break;
            }

            var numCars = car.trainset.cars.Count;
            var handBrakesRequired = Mathf.CeilToInt(numCars * Main.settings.handbrakeSpawnPercent / 100f);
            Main.DebugLog(() => $"{car.ID} setting {handBrakesRequired} handbrakes in trainset");
            foreach (var carToSet in car.trainset.cars.Take(handBrakesRequired))
                carToSet.GetComponent<CabooseController>().SetIndependentBrake(1f);
            foreach (var carToRelease in car.trainset.cars.Skip(handBrakesRequired))
                carToRelease.GetComponent<CabooseController>().SetIndependentBrake(0f);
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
                if (Mathf.Abs(PlayerManager.Car.GetForwardSpeed()) > 3.6f)
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
                static bool IsFinalInList(TransportTask transportTask, IEnumerable<Task> tasks)
                {
                    return tasks.LastOrDefault() switch
                    {
                        SequentialTasks sequential => IsFinalInList(transportTask, sequential.tasks),
                        ParallelTasks parallel => parallel.tasks.Contains(transportTask),
                        Task task => task == transportTask,
                    };
                }
                return IsFinalInList(transportTask, transportTask.Job.tasks);
            }

            private static bool IsHandbrakeSet(Car car)
            {
                var trainCar = SingletonBehaviour<IdGenerator>.Instance.logicCarToTrainCar[car];
                return trainCar.brakeSystem.independentBrakePosition >= 0.5f;
            }

            public static void Postfix(TransportTask __instance, ref TaskState __result)
            {
                if (__instance.state != TaskState.Done)
                    return;
                if (!IsFinalTask(__instance))
                    return;
                var cars = __instance.cars;
                var numRequired = Mathf.CeilToInt(cars.Count * Main.settings.handbrakeRatioRequired / 100f);
                var withBrakeSet = cars.Count(IsHandbrakeSet);
                Main.DebugLog(() => $"{__instance.Job.ID}: numCars={cars.Count},numRequired={numRequired},withBrakeSet={withBrakeSet}");
                if (withBrakeSet < numRequired)
                {
                    __instance.SetState(TaskState.InProgress);
                    __result = __instance.state;
                }
            }

            private static void DumpJob(Job job)
            {
                static void DumpTask(Task task, int indent = 0)
                {
                    var data = task.GetTaskData();
                    Main.DebugLog(() => $"{string.Concat(Enumerable.Repeat(" ", indent))}{data.type},{data.warehouseTaskType},{data.state},{data.destinationTrack},{string.Join(",", (data.cars ?? new List<Car>()).Select(c=>c.ID))}");
                    foreach (var subtask in data.nestedTasks ?? new List<Task>())
                        DumpTask(subtask, indent + 1);
                }
                foreach (var task in job.tasks)
                    DumpTask(task);
            }
        }
    }
}
