using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimedieval
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            RimedievalMod.harmony.PatchAll();

            MethodInfo allowedPrecepts = AccessTools.Method(typeof(HarmonyPatches), nameof(HarmonyPatches.AllowedPrecepts));
            foreach (Type preceptWorkerType in typeof(PreceptWorker).AllSubclasses().AddItem(typeof(PreceptWorker)))
            {
                try
                {
                    MethodInfo method = AccessTools.Method(preceptWorkerType, "get_ThingDefs");
                    RimedievalMod.harmony.Patch(method, null, postfix: new HarmonyMethod(allowedPrecepts));
                }
                catch
                {
                }
            }
            MethodInfo filterItems = AccessTools.Method(typeof(HarmonyPatches), nameof(HarmonyPatches.FilterItems));
            foreach (var subType in typeof(ThingSetMaker).AllSubclasses())
            {
                try
                {
                    var method = AccessTools.Method(subType, "Generate", new Type[] { typeof(ThingSetMakerParams), typeof(List<Thing>) });
                    RimedievalMod.harmony.Patch(method, null, postfix: new HarmonyMethod(filterItems));
                }
                catch { }
            }
        }

        public static IEnumerable<PreceptThingChance> AllowedPrecepts(IEnumerable<PreceptThingChance> __result)
        {
            return __result.Where(x => x.def.IsAllowedForRimedieval() && x.chance > 0);
        }

        public static void FilterItems(ThingSetMakerParams __0, List<Thing> __1)
        {
            var count = __1.RemoveAll(x => x.def.IsAllowedForRimedieval() is false);
            if (count > 0)
            {
                Log.Message("Removed " + count);
            }
        }
    }

    [HarmonyPatch(typeof(ThingSetMakerUtility), "GetAllowedThingDefs")]
    public static class GetAllowedThingDefs_Patch
    {
        public static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> __result)
        {
            return __result.GetAllowedThingDefs();
        }
    }



    [HarmonyPatch(typeof(SymbolResolver_SingleThing), "Resolve")]
    public static class SymbolResolver_SingleThing_Resolve_Patch
    {
        public static bool Prefix(ResolveParams rp)
        {
            if (rp.singleThingDef.IsAllowedForRimedieval() is false || rp.singleThingToSpawn != null && rp.singleThingToSpawn.def.IsAllowedForRimedieval() is false)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(RaidStrategyWorker), "MinimumPoints")]
    public static class MinimumPoints_Patch
    {
        public static void Prefix(ref Faction faction, ref PawnGroupKindDef groupKind)
        {
            if (faction is null)
            {
                faction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("Pirate"));
            }
            if (groupKind is null)
            {
                groupKind = PawnGroupKindDefOf.Combat;
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival))]
    [HarmonyPatch("TryExecuteWorker")]
    public static class IncidentWorker_OrbitalTraderArrival_TryExecuteWorkerPatch
    {
        public static bool Prefix(IncidentParms parms)
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_ShipChunkDrop), "CanFireNowSub")]
    public class Patch_CanFireNowSub
    {
        [HarmonyPriority(Priority.First)]
        public static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(StorytellerComp_ShipChunkDrop), "MakeIntervalIncidents")]
    public class Patch_MakeIntervalIncidents
    {
        [HarmonyPriority(Priority.Last)]
        public static IEnumerable<FiringIncident> Postfix(IEnumerable<FiringIncident> __result)
        {
            yield break;
        }
    }

    [HarmonyPatch(typeof(SymbolResolver_AncientCryptosleepCasket), "Resolve")]
    public class SymbolResolver_AncientCryptosleepCasket_Resolve
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();
            MethodInfo method = AccessTools.Method(typeof(ThingMaker), "MakeThing");
            MethodInfo methodToCall = AccessTools.Method(typeof(SymbolResolver_AncientCryptosleepCasket_Resolve), "GetRandomStuff");
            bool found = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (!found && codes[i].opcode == OpCodes.Ldnull && codes[i + 1].Calls(method))
                {
                    found = true;
                    yield return new CodeInstruction(OpCodes.Call, methodToCall);
                }
                else
                {
                    yield return codes[i];
                }
            }
        }

        public static ThingDef GetRandomStuff()
        {
            return GenStuff.RandomStuffFor(ThingDefOf.AncientCryptosleepCasket);
        }
    }
}
