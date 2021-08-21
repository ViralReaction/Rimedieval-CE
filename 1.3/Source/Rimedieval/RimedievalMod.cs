﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Rimedieval
{
    class RimedievalMod : Mod
    {
        public const string ModName = "Rimedieval";
        public static RimedievalSettings settings;

        public static Harmony harmony;
        public RimedievalMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<RimedievalSettings>();
            harmony = new Harmony("Ogam.Rimedieval");
            var methodToCall = AccessTools.Method(typeof(HarmonyPatches), nameof(HarmonyPatches.AllowedThings));
            foreach (var stockGeneratorType in typeof(StockGenerator).AllSubclasses())
            {
                try
                {
                    var method = AccessTools.Method(stockGeneratorType, "GenerateThings");
                    harmony.Patch(method, null, new HarmonyMethod(methodToCall));
                    Log.Message("Patched " + method + " - " + methodToCall);
                }
                catch
                {
            
                }
            }
        }


        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }
        public override string SettingsCategory()
        {
            return ModName;
        }
        public override void WriteSettings()
        {
            base.WriteSettings();
            DefsAlterer.DoDefsAlter();
        }
    }
    [StaticConstructorOnStartup]
    public static class DefsAlterer
    {
        static DefsAlterer()
        {
            Setup();
            DoDefsAlter();
        }
        public static void Setup()
        {
            if (!RimedievalMod.settings.rimedievalMechAddonWasLoaded && ModLister.HasActiveModWithName(RimedievalSettings.RimedievalMechAddonModName))
            {
                RimedievalMod.settings.rimedievalMechAddonWasLoaded = true;
                RimedievalMod.settings.disableMechanoids = false;
            }
            RemoveTechHediffs();
        }

        public static void RemoveTechHediffs()
        {
            var defs = DefDatabase<ThingDef>.AllDefs.Where(x => x.isTechHediff && 
            ((x.costList?.Any(y => y.thingDef == ThingDefOf.Plasteel || y.thingDef == ThingDefOf.ComponentSpacer) ?? false)
            || x.techLevel > TechLevel.Industrial
            || (x.recipeMaker?.recipeUsers?.Any(z => z == ThingDef.Named("TableMachining")) ?? false)
            )).ToList();
            MethodInfo methodInfo = AccessTools.Method(typeof(DefDatabase<ThingDef>), "Remove", null, null);
            foreach (var def in defs)
            {
                methodInfo.Invoke(null, new object[]
                {
                    def
                });
            }
        }
        public static void DoDefsAlter()
        {

        }
    }
}