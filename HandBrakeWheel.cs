using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HandBrake
{
    public static class HandBrakeWheel
    {
        private readonly struct WheelPosition
        {
            public readonly Vector3 position;
            public readonly Vector3 back;
            public readonly float size;

            public WheelPosition(Vector3 position, Vector3 back, float size)
            {
                this.position = position;
                this.back = back;
                this.size = size;
            }
        }

        private static readonly WheelPosition autorackPosition =
            new WheelPosition(new Vector3(-1.4f, 0.8f, 1f), Vector3.right, 1.0f);
        private static readonly WheelPosition boxcarPosition =
            new WheelPosition(new Vector3(0.25f, 3.42f, 6.58f), Vector3.back, 1.15f);
        private static readonly WheelPosition flatbedPosition =
            new WheelPosition(new Vector3(-1.37f, 0.8f, 1f), Vector3.right, 0.8f);
        private static readonly WheelPosition gondolaPosition =
            new WheelPosition(new Vector3(-0.9f, 2.4f, -6.1f), Vector3.forward, 1.0f);
        private static readonly WheelPosition hopperPosition =
            new WheelPosition(new Vector3(-0.495f, 1.86f, -8.56f), Vector3.forward, 0.97f);
        private static readonly WheelPosition passengerPosition =
            new WheelPosition(new Vector3(0.8f, 2f, -12.12f), Vector3.forward, 1.0f);
        private static readonly WheelPosition slicedPassengerPosition =
            new WheelPosition(new Vector3(0.8f, 2f, -7.13f), Vector3.forward, 1.0f);
        private static readonly WheelPosition tankPosition =
            new WheelPosition(new Vector3(-0.715f, 1.78f, -6.39f), Vector3.forward, 1.02f);

        private static readonly Dictionary<TrainCarType, WheelPosition> wheelPositions = new Dictionary<TrainCarType, WheelPosition>()
        {
            [TrainCarType.FlatbedEmpty] = flatbedPosition,
            [TrainCarType.FlatbedStakes] = flatbedPosition,
            [TrainCarType.FlatbedMilitary] = flatbedPosition,
            [TrainCarType.AutorackRed] = autorackPosition,
            [TrainCarType.AutorackBlue] = autorackPosition,
            [TrainCarType.AutorackGreen] = autorackPosition,
            [TrainCarType.AutorackYellow] = autorackPosition,
            [TrainCarType.TankOrange] = tankPosition,
            [TrainCarType.TankWhite] = tankPosition,
            [TrainCarType.TankYellow] = tankPosition,
            [TrainCarType.TankBlue] = tankPosition,
            [TrainCarType.TankChrome] = tankPosition,
            [TrainCarType.TankBlack] = tankPosition,
            [TrainCarType.BoxcarBrown] = boxcarPosition,
            [TrainCarType.BoxcarGreen] = boxcarPosition,
            [TrainCarType.BoxcarPink] = boxcarPosition,
            [TrainCarType.BoxcarRed] = boxcarPosition,
            [TrainCarType.BoxcarMilitary] = boxcarPosition,
            [TrainCarType.RefrigeratorWhite] = boxcarPosition,
            [TrainCarType.HopperBrown] = hopperPosition,
            [TrainCarType.HopperTeal] = hopperPosition,
            [TrainCarType.HopperYellow] = hopperPosition,
            [TrainCarType.GondolaRed] = gondolaPosition,
            [TrainCarType.GondolaGreen] = gondolaPosition,
            [TrainCarType.GondolaGray] = gondolaPosition,
            [TrainCarType.NuclearFlask] = flatbedPosition,
        };

        static HandBrakeWheel()
        {
            var position = (UnityModManager.FindMod("SlicedPassengerCars")?.Enabled ?? false) ? slicedPassengerPosition : passengerPosition;
            wheelPositions[TrainCarType.PassengerRed] = position;
            wheelPositions[TrainCarType.PassengerGreen] = position;
            wheelPositions[TrainCarType.PassengerBlue] = position;
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
            foreach (var other in wheel.GetComponentsInParent<Collider>())
                Physics.IgnoreCollision(collider, other);
        }

        public static void AddWheelToCar(TrainCar car)
        {
            if (car.GetComponent<CabInputCaboose>() != null)
                return;
            if (!wheelPositions.TryGetValue(car.carType, out var wheelPosition))
                return;
            var cabInput = car.gameObject.AddComponent<CabInputCaboose>();
            var control = Object.Instantiate(
                WheelControl,
                car.transform.TransformPoint(wheelPosition.position),
                car.transform.rotation * Quaternion.LookRotation(Vector3.up, wheelPosition.back),
                car.transform.GetComponentInChildren<LODGroup>().transform)!;

            ReplaceCollider(control);

            control.transform.localScale = new Vector3(wheelPosition.size, 1.0f, wheelPosition.size);
            control.SetLayersRecursive("Interactable");
            foreach (var collider in control.transform.GetComponentsInChildren<Collider>())
                collider.isTrigger = true;
            cabInput.independentBrake = control;
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