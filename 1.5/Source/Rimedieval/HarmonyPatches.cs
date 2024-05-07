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

    [HarmonyPatch(typeof(ThingStuffPair), "Commonality", MethodType.Getter)]
    public class Commonality_Patch
    {
        public static Pawn pawnToLookInto;
        public static bool Prefix(ThingStuffPair __instance, ref float __result)
        {
            Faction faction = pawnToLookInto?.Faction;
            if (faction != null)
            {
                if (faction.def.techLevel > TechLevel.Medieval)
                {
                    FactionTracker.Instance.SetNewTechLevelForFaction(faction.def);
                }
                if (faction.def.techLevel <= TechLevel.Medieval && DefCleaner.GetTechLevelFor(__instance.thing) > TechLevel.Medieval)
                {
                    __result = 0f;
                    return false;
                }
            }
            return true;
        }
        public static void Postfix(ThingStuffPair __instance, ref float __result)
        {
            if (pawnToLookInto?.Faction != null && !pawnToLookInto.Faction.IsPlayer)
            {
                if (pawnToLookInto.Faction.def.techLevel <= TechLevel.Medieval && __instance.thing.IsRangedWeapon && __instance.thing.Verbs.Any(x => x.muzzleFlashScale > 0))
                {
                    if (__result > 0)
                    {
                        __result *= 0.25f;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Faction), "GetReportText", MethodType.Getter)]
    public static class GetReportText_Patch
    {
        public static void Postfix(Faction __instance, ref string __result)
        {
            if (__instance.def.techLevel > TechLevel.Medieval)
            {
                FactionTracker.Instance.SetNewTechLevelForFaction(__instance.def);
            }
            __result += "\n\n" + "RM.FactionTechLevelInfo".Translate(__instance.def.techLevel.ToStringHuman());
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

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy))]
    [HarmonyPatch("FactionCanBeGroupSource")]
    public static class FactionCanBeGroupSourcePatch
    {
        public static void Postfix(ref bool __result, Faction f)
        {
            if (__result && f?.def == FactionDefOf.Mechanoid && RimedievalMod.settings.disableMechanoids)
            {
                __result = false;
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

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy))]
    [HarmonyPatch("TryExecuteWorker")]
    public static class TryExecuteWorkerPatch
    {
        public static bool Prefix(IncidentParms parms)
        {
            return parms.faction == null || parms.faction.def != FactionDefOf.Mechanoid || !RimedievalMod.settings.disableMechanoids;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy))]
    [HarmonyPatch("TryResolveRaidFaction")]
    public static class TryResolveRaidFactionPatch
    {
        public static void Postfix(ref bool __result, IncidentParms parms)
        {
            if (__result && parms.faction?.def == FactionDefOf.Mechanoid && RimedievalMod.settings.disableMechanoids)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("SpawnSetup")]
    public static class Pawn_SpawnSetup
    {
        public static void Postfix(Pawn __instance)
        {
            if (RimedievalMod.settings.disableMechanoids && __instance.RaceProps.IsMechanoid)
            {
                __instance.Destroy();
            }
        }
    }

    [HarmonyPatch(typeof(GenStep_MechCluster))]
    [HarmonyPatch("Generate")]
    public static class Generate
    {
        public static bool Prefix()
        {
            return !RimedievalMod.settings.disableMechanoids;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_MechCluster))]
    [HarmonyPatch("TryExecuteWorker")]
    public static class TryExecuteWorker
    {
        public static bool Prefix(ref bool __result)
        {
            if (RimedievalMod.settings.disableMechanoids)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MechClusterUtility))]
    [HarmonyPatch("SpawnCluster")]
    public static class SpawnCluster
    {
        public static bool Prefix(ref List<Thing> __result)
        {
            if (RimedievalMod.settings.disableMechanoids)
            {
                __result = new List<Thing>();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(QuestNode_GetFaction))]
    [HarmonyPatch("IsGoodFaction")]
    public static class IsGoodFaction_Patch
    {
        public static bool Prefix(ref bool __result, Faction faction, Slate slate)
        {
            if (RimedievalMod.settings.disableMechanoids && faction == Faction.OfMechanoids)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ThreatsGenerator))]
    [HarmonyPatch("GetPossibleIncidents")]
    public static class GetPossibleIncidents_Patch
    {
        public static IEnumerable<IncidentDef> Postfix(IEnumerable<IncidentDef> __result)
        {
            foreach (IncidentDef r in __result)
            {
                if (r == IncidentDefOf.MechCluster && RimedievalMod.settings.disableMechanoids)
                {
                    continue;
                }
                else
                {
                    yield return r;
                }
            }
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
