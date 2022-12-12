using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;

namespace QuantumGunners
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static BepInEx.Logging.ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("QuantumGunners");
        private void Awake()
        {

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(AIControl), "CanFireWeaponType")]
        [HarmonyPostfix]
        public static void CanFireWeaponType(TWeapon weap, ref bool __result) {
            if (weap.type == WeaponType.Pulse) {
                __result = true;
                log.LogInfo("Can fire Quantum Pulse!");
            }
        }

        [HarmonyPatch(typeof(AIControl), "FireAllWeapons")]
        [HarmonyPrefix]
        public static void FireAllWeapons(float targetDistance, AIControl __instance) {
            Transform weaponsTransform = (Transform) Traverse.Create(__instance).Field("weaponsTransform").GetValue();
            bool fireTriggerReady = (bool) Traverse.Create(__instance).Field("fireTriggerReady").GetValue();

            for (int i = 0; i < weaponsTransform.childCount; i++)
            {
                Weapon component = weaponsTransform.GetChild(i).GetComponent<Weapon>();
                if (component.wRef.type == WeaponType.Pulse) {
                    log.LogInfo(component.turretMounted + " : " + targetDistance + " : " + component.wRef.aoe);
                }
                if (component.turretMounted < 0 && component.wRef.type == WeaponType.Pulse)
                {
                    if (targetDistance <= (float)component.wRef.aoe)
                    {
                        component.Fire(__instance.target, fireTriggerReady);
                    }
                    else
                    {
                        component.HoldFire();
                    }
                }
		}
        }
        

        [HarmonyPatch(typeof(WeaponTurret), "CanFireWeaponType")]
        [HarmonyPostfix]
        public static void CanFireWeaponTypeTurret(TWeapon weap, ref bool __result, WeaponTurret __instance) {
            if (weap.type == WeaponType.Pulse) {
                if (__instance.target.CompareTag("Projectile") || __instance.target.CompareTag("Drone"))
                {
                    if (weap.canHitProjectiles) {
                        __result = true;
                    } else {
                        __result = false;
                    }
                } else {
                    __result = true;
                }
            }
        }


        // GetDesiredDistanceAndWeaponTypes
        [HarmonyPatch(typeof(WeaponTurret), "GetDesiredDistanceAndWeaponTypes")]
        [HarmonyPostfix]
        public static void GetDesiredDistanceAndWeaponTypes(WeaponTurret __instance) {
            SpaceShip ss = (SpaceShip) Traverse.Create(__instance).Field("ss").GetValue();
            for (int i = 0; i < ss.weapons.Count; i++)
            {
                Weapon weapon = ss.weapons[i];
                if (weapon.turretMounted == __instance.turretIndex)
                {
                    if (weapon.wRef.type == WeaponType.Pulse) {
                        Traverse.Create(__instance).Field("firingBeamWeapon").SetValue(true);
                        Traverse.Create(__instance).Field("desiredDistance").SetValue(weapon.wRef.aoe);
                    }
                }
            }
        }


        // IL Patch
        private static FieldInfo firingBeamWeapon = typeof(WeaponTurret).GetField("firingBeamWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo get_range2 = typeof(Weapon).GetMethod("get_range2", BindingFlags.Instance | BindingFlags.Public);

        private static bool AddedCheck(bool check, Weapon weapon, float num) {
            if (weapon.wRef.type != WeaponType.Pulse) return check;
            return check || weapon.wRef.aoe >= num;
        }
        private static MethodInfo AddedCheckInfo = typeof(Plugin).GetMethod("AddedCheck", BindingFlags.NonPublic | BindingFlags.Static);

        [HarmonyPatch(typeof(WeaponTurret), "Update")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var cursor = new IlCursor(instructions);

            if (!cursor.JumpPast(instruction => instruction.LoadsField(firingBeamWeapon)))
            {
                log.LogError("Failed to find !firingBeamWeapon");
                return cursor.GetInstructions();
            }
            if (!cursor.JumpPast(instruction => instruction.Calls(get_range2)))
            {
                log.LogError("Failed to find call to get_range2");
                return cursor.GetInstructions();
            }
            if (!cursor.JumpPast(instruction => instruction.opcode == OpCodes.Ceq))
            {
                log.LogError("Failed to find ceq");
                return cursor.GetInstructions();
            }

            // Check is already on the stack, equal to whether weapon was in range normally
            cursor.Emit(new CodeInstruction(OpCodes.Ldloc_S, 18)); // Add the Weapon to the stack
            cursor.Emit(new CodeInstruction(OpCodes.Ldloc_S, 11)); // Add the num (distance to the target) to the stack
            cursor.Emit(new CodeInstruction(OpCodes.Call, AddedCheckInfo)); // Call our check function, replacing the old "Check" with its result

            return cursor.GetInstructions();
        }
    }
}
