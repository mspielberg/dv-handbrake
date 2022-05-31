using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HandBrake
{
    public static class HandBrakeWheel
    {
        private class WheelPosition
        {
            public readonly Vector3 position;
            public readonly Vector3 back;
            public readonly float size;
            public readonly string[] patterns;

            public WheelPosition(Vector3 position, Vector3 back, float size, params string[] patterns)
            {
                this.position = position;
                this.back = back;
                this.size = size;
                this.patterns = patterns;
            }
        }

        private static readonly List<WheelPosition> wheelPositions = new List<WheelPosition>()
        {
            new WheelPosition(new Vector3(-1.4f, 0.8f, 1f), Vector3.right, 1.0f, "Autorack"),
            new WheelPosition(new Vector3(0.25f, 3.42f, 6.58f), Vector3.back, 1.15f, "Boxcar", "Refrigerator"),
            new WheelPosition(new Vector3(-1.37f, 0.8f, 1f), Vector3.right, 0.8f, "Flatbed", "NuclearFlask"),
            new WheelPosition(new Vector3(-1.41f, 1.3f, 5.7f), Vector3.right, 1.0f, "Gondola"),
            new WheelPosition(new Vector3(-0.495f, 1.86f, -8.56f), Vector3.forward, 0.97f, "Hopper"),
            new WheelPosition(new Vector3(-0.715f, 1.78f, -6.39f), Vector3.forward, 1.02f, "Tank"),
        };

        private static readonly WheelPosition passengerPosition =
            new WheelPosition(new Vector3(0.8f, 2f, -10.12f), Vector3.forward, 1.0f, "Passenger");
        private static readonly WheelPosition slicedPassengerPosition =
            new WheelPosition(new Vector3(0.8f, 2f, -7.13f), Vector3.forward, 1.0f, "Passenger");

        private static readonly Dictionary<TrainCarType, WheelPosition> wheelPositionMap = new Dictionary<TrainCarType, WheelPosition>();

        static HandBrakeWheel()
        {
            var passengerCarPosition = (UnityModManager.FindMod("SlicedPassengerCars")?.Enabled ?? false) ? slicedPassengerPosition : passengerPosition;
            wheelPositions.Add(passengerCarPosition);
            foreach (TrainCarType carType in System.Enum.GetValues(typeof(TrainCarType)))
            {
                var name = System.Enum.GetName(typeof(TrainCarType), carType);
                var wheelPosition = wheelPositions.FirstOrDefault(wp => wp.patterns.Any(name.StartsWith));
                if (wheelPosition != default)
                    wheelPositionMap.Add(carType, wheelPosition);
            }
        }

        private static GameObject? _wheelControl;
        private static GameObject WheelControl
        {
            get
            {
                if (_wheelControl == null)
                {
                    var caboosePrefab = CarTypes.GetCarPrefab(TrainCarType.CabooseRed);
                    var cabooseInteriorPrefab = caboosePrefab.GetComponent<TrainCar>().interiorPrefab;
                    _wheelControl = cabooseInteriorPrefab.transform.Find("C BrakeWheel").gameObject;
                }
                return _wheelControl;
            }
        }

        private static Transform? GetPivot(TrainCar car)
        {
            if (car.GetComponent<CabInputCaboose>() != null)
            {
                Main.DebugLog(() => $"{car.ID} already has a CabInputCaboose component");
                return null;
            }

            if (car.transform.Find("[DvMod.HandBrake]") is Transform transform)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    transform.GetChild(i).gameObject.SetActive(false);
                }
                transform.parent = car.interior;
                return transform;
            }

            if (wheelPositionMap.TryGetValue(car.carType, out var wheelPosition))
            {
                Main.DebugLog(() => $"Found defined WheelPosition for {car.carType}");
                var obj = new GameObject("[DvMod.HandBrake]");
                obj.transform.parent = car.interior;
                obj.transform.localPosition = wheelPosition.position;
                obj.transform.localRotation = Quaternion.LookRotation(Vector3.up, wheelPosition.back);
                obj.transform.localScale = new Vector3(wheelPosition.size, 1.0f, wheelPosition.size);
                return obj.transform;
            }

            Main.DebugLog(() => $"Found no position for handbrake wheel for {car.carType}");
            return null;
        }

        private static void ReplaceCollider(GameObject wheel)
        {
            GameObject.Destroy(wheel.transform.Find("colliders").gameObject);
            var colliderGO = new GameObject("collider");
            var collider = colliderGO.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            colliderGO.transform.parent = wheel.transform;
            colliderGO.transform.localPosition = Vector3.zero;
            colliderGO.transform.localRotation = Quaternion.identity;
            colliderGO.transform.localScale = new Vector3(0.35f, 0.01f, 0.35f);
        }

        public static void AddWheelToCar(TrainCar car)
        {
            var pivotTransform = GetPivot(car);
            if (pivotTransform == null)
                return;

            var cabInput = car.gameObject.AddComponent<CabInputCaboose>();
            var control = Object.Instantiate(
                WheelControl,
                pivotTransform.position,
                pivotTransform.rotation,
                pivotTransform);

            ReplaceCollider(control);

            control.SetLayersRecursive("Interactable");
            cabInput.independentBrake = control;

            if (car.GetComponent<TrainPhysicsLod>() is var lodController && lodController != null)
                lodController.TrainPhysicsLodChanged += (currentLod) => control.SetActive(currentLod <= 1);
        }

        [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.Awake))]
        public static class AwakePatch
        {
            public static void Postfix(TrainCar __instance)
            {
                if (Main.settings.addWheels)
                    AddWheelToCar(__instance);
            }
        }

        [HarmonyPatch(typeof(ControlsInstantiator), nameof(ControlsInstantiator.Spawn))]
        public static class SpawnPatch
        {
            public static void Prefix(ControlSpec spec)
            {
                if (spec.name == "C BrakeWheel(Clone)" && spec is Wheel wheel)
                {
                    wheel.scrollWheelHoverScroll = 1f;
                    wheel.nonVrStaticInteractionArea = null;
                }
            }
        }

        [HarmonyPatch(typeof(CabooseController), nameof(CabooseController.SetIndependentBrake))]
        public static class SetIndependentBrakePatch
        {
            public static void Postfix(CabooseController __instance)
            {
                __instance.car.brakeSystem.independentBrakePosition =
                    Mathf.Clamp01(Mathf.InverseLerp(0.5f, 0.95f, __instance.targetIndependentBrake));
            }
        }
    }
}
