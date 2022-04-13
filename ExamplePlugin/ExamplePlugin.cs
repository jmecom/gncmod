using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using MonoMod.Cil;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Xml.Linq;
using Zio;
using HG;

namespace ExamplePlugin
{
	//This is an example plugin that can be put in BepInEx/plugins/ExamplePlugin/ExamplePlugin.dll to test out.
    //It's a small plugin that adds a relatively simple item to the game, and gives you that item whenever you press F2.

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]
	
	//This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	
	//We will be using 2 modules from R2API: ItemAPI to add our item and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI))]
	
	//This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class ExamplePlugin : BaseUnityPlugin
	{
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "jm";
        public const string PluginName = "GNCMod";
        public const string PluginVersion = "1.0.0";

        private static int runCounter;
        private string TmpRunsDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "GNCModRuns");
        private static readonly UPath historyDirectory = new UPath("/RunReports/GNCModHistory/");
        private static IFileSystem storage => RoR2Application.fileSystem;

        // private void RegisterPenniesNerf()
        // {
        //     IL.RoR2.HealthComponent.TakeDamage += (il) =>
        //     {
        //         ILCursor c = new ILCursor(il);
        //             c.GotoNext(
        //                 x => x.MatchLdarg(0),
        //                 x => x.MatchCallvirt<HealthComponent>("get_combinedHealth"),
        //                 x => x.MatchLdarg(0),
        //                 x => x.MatchCallvirt<HealthComponent>("get_fullCombinedHealth"),
        //                 x => x.MatchLdcR4(0.9f)
        //                 );
        //             c.Index += 4;
        //     };
        // }

        private static Stream GetHistoryFile(string fileName)
        {
            UPath path = historyDirectory / fileName;
            storage.CreateDirectory(historyDirectory);
            return storage.OpenFile(path, FileMode.Create, FileAccess.Write);
        }

        private void RegisterSaveRunReportAfterWave()
        {
            Log.LogInfo("Registering SaveRunReportAfterWave");
            runCounter = 0;
            On.RoR2.InfiniteTowerRun.BeginNextWave += (orig, self) =>
            {
                orig(self);
                GameEndingDef fakeEnding = new GameEndingDef(); 
                RunReport runReport = RunReport.Generate(self, fakeEnding);

                StringBuilder stringBuilder = HG.StringBuilderPool.RentStringBuilder();
                string fileName = stringBuilder
                    .Append(runReport.runGuid)
                    .Append("-")
                    .Append((++runCounter).ToString())
                    .Append(".xml").ToString();

                HG.StringBuilderPool.ReturnStringBuilder(stringBuilder);
                using Stream stream = GetHistoryFile(fileName);
                if (stream != null) {
                    XDocument xDocument = new XDocument();
                    xDocument.Add(HGXml.ToXml("RunReport", runReport));
                    xDocument.Save(stream);
                    stream.Flush();
                    stream.Dispose();
                }
            };
        }

        private void RegisterModifyDropTable()
        {
            Log.LogInfo("Registering ModifyDropTable");

            On.RoR2.PickupDropTable.GenerateUniqueDrops += (orig, self, maxDrops, rng) =>
            {
                Log.LogInfo("Hooking drop table...");
                
                RoR2.InfiniteTowerRun run = RoR2.Run.instance as RoR2.InfiniteTowerRun;
                
                // Remove roll of pennies.
                ItemDef[] originalBlacklist = run.blacklistedItems.Clone() as ItemDef[];
                List<ItemDef> blacklist = new List<ItemDef>();
                foreach (var item in originalBlacklist) blacklist.Add(item);
                blacklist.Add(RoR2.DLC1Content.Items.GoldOnHurt);
                run.blacklistedItems = blacklist.ToArray();

                maxDrops++;  // Extra choice.
                self.canDropBeReplaced = false;  // No lunar replacements.

                PickupIndex[] result = orig(self, maxDrops, rng);

                // Wave 30 should guarantee only red items.
                if (run.waveController.waveIndex == 30) {
                    var legendaries = RoR2.ItemCatalog.tier3ItemList;
                    run.waveController.rewardDisplayTier = ItemTier.Tier3;
                    for (int i = 0; i < result.Length; i++) {
                        result[i] = PickupCatalog.FindPickupIndex(legendaries[rng.RangeInt(0, legendaries.Count - 1)]);
                    }
                    return result;
                }

                // TODO
                // Mix elite equipments and lunar items into the regular drop table.
                // BasicPickupDropTable customDropTable = new BasicPickupDropTable();
                // customDropTable.tier1Weight = 0.8f;
                // customDropTable.tier2Weight = 0.1f;
                // run.waveController.rewardDropTable

                // 10% chance to replace an item with either an elite equipment or blue item.
                int roll = rng.RangeInt(0, 10);  // max is exclusive.
                if (roll >= 1) {   
                    return result;
                }                
                
                // PickupIndex[] eliteEquipments = {
                //     PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixBlue.equipmentIndex),
                //     PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixHaunted.equipmentIndex),
                //     PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixLunar.equipmentIndex),
                //     PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixPoison.equipmentIndex),
                //     PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixRed.equipmentIndex),
                //     PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixWhite.equipmentIndex),
                //     PickupCatalog.FindPickupIndex(RoR2.DLC1Content.Buffs.EliteEarth.eliteDef.eliteEquipmentDef.equipmentIndex),
                //     // PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixEcho.equipmentIndex),
                //     // PickupCatalog.FindPickupIndex(RoR2.DLC1Content.Equipment.EliteVoidEquipment.equipmentIndex),
                // };

                List<PickupIndex> specialItems = new List<PickupIndex>();
                specialItems.Add(PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixBlue.equipmentIndex));
                specialItems.Add(PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixHaunted.equipmentIndex));
                specialItems.Add(PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixLunar.equipmentIndex));
                specialItems.Add(PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixPoison.equipmentIndex));
                specialItems.Add(PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixRed.equipmentIndex));
                specialItems.Add(PickupCatalog.FindPickupIndex(RoR2.RoR2Content.Equipment.AffixWhite.equipmentIndex));
                specialItems.Add(PickupCatalog.FindPickupIndex(RoR2.DLC1Content.Buffs.EliteEarth.eliteDef.eliteEquipmentDef.equipmentIndex));

                var lunars = RoR2.ItemCatalog.lunarItemList;
                foreach (var lunar in lunars) {
                    specialItems.Add(PickupCatalog.FindPickupIndex(lunar));
                }

                result[0] = specialItems[rng.RangeInt(0, specialItems.Count - 1)];
                
                // result[0] = PickupCatalog.FindPickupIndex(RoR2.DLC1Content.Buffs.EliteEarth.eliteDef.eliteEquipmentDef.equipmentIndex);
                // result[0] = eliteEquipments[rng.RangeInt(0, eliteEquipments.Length - 1)];
                // Log.LogInfo("Added " + result[0]);
                
                // foreach (var v in result) {
                //     Log.LogInfo(v);
                //     Log.LogInfo(v.pickupDef.internalName);
                // }

                return result;
            };

        }

		//The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            // RegisterPenniesNerf();
            RegisterSaveRunReportAfterWave();
            RegisterModifyDropTable();    
        }

        //The Update() method is run on every frame of the game.
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Log.LogInfo("F2 pushed");
                var run = RoR2.Run.instance as RoR2.InfiniteTowerRun;
                var controller = run.waveController;
                controller.DropRewards();

                // BasicPickupDropTable table = controller.rewardDropTable as BasicPickupDropTable;
                // Log.LogInfo(table);
                // Log.LogInfo(table.GetType());

                // Log.LogInfo("tier1Weight = " + table.tier1Weight);
                // Log.LogInfo("tier2Weight = " + table.tier2Weight);
                // Log.LogInfo("tier3Weight = " + table.tier3Weight);
                // Log.LogInfo("bossWeight = " + table.bossWeight);
                // Log.LogInfo("lunarEquipmentWeight = " + table.lunarEquipmentWeight);
                // Log.LogInfo("lunarItemWeight = " + table.lunarItemWeight);
                // Log.LogInfo("lunarCombinedWeight = " + table.lunarCombinedWeight);
                // Log.LogInfo("equipmentWeight = " + table.equipmentWeight);
                // Log.LogInfo("voidTier1Weight = " + table.voidTier1Weight);
                // Log.LogInfo("voidTier2Weight = " + table.voidTier2Weight);
                // Log.LogInfo("voidTier3Weight = " + table.voidTier3Weight);
                // Log.LogInfo("voidBossWeight = " + table.voidBossWeight);

                // Log.LogInfo("rewardOptionCount = " + controller.rewardOptionCount);
                // Log.LogInfo("rewardDisplayTier = " + controller.rewardDisplayTier);

                // // Doesn't work -- not sure if `table` is a reference or what?
                // table.tier1Weight = 0;
                // table.tier2Weight = 0;
                // table.tier3Weight = 90;

                // Log.LogInfo("[1] ref: " + table.GetInstanceID() + " " + table.GetHashCode());
                // Log.LogInfo("[2] ref: " + (RoR2.Run.instance as RoR2.InfiniteTowerRun).waveController.rewardDropTable.GetInstanceID() +
                //     " " + (RoR2.Run.instance as RoR2.InfiniteTowerRun).waveController.rewardDropTable.GetHashCode());

                
            }
        }
    }
}
