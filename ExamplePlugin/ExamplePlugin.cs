using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using MonoMod.Cil;

using System;
using System.IO;
using System.Text;
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

        // public PickupIndex[] PickupDropTable_GenerateUniqueDrops(int maxDrops, Xoroshiro128Plus rng)
        // {
        //     PickupIndex[] array = GenerateUniqueDropsPreReplacement(maxDrops, rng);
        //     if (canDropBeReplaced)
        //     {
        //         RandomlyLunarUtils.CheckForLunarReplacementUniqueArray(array, rng);
        //     }
        //     return array;
        // }

        private static PickupIndex[] Hooked_DropRewards(int maxDrops, Xoroshiro128Plus rng)
        {
            PickupIndex[] array2 = new PickupIndex[3];
            return array2;
        }

        private void RegisterModifyDropTable()
        {
            Log.LogInfo("Registering ModifyDropTable");

            On.RoR2.BasicPickupDropTable.GenerateUniqueDropsPreReplacement += (orig, self, maxDrops, rng) =>
            {
                Log.LogInfo("Hooking drop table...");
                return orig(self, maxDrops, rng);
                // PickupIndex[] array2 = new PickupIndex[3];
                // return array2;
            };
        }

		//The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            // RegisterPenniesNerf();
            RegisterModifyDropTable();
            RegisterSaveRunReportAfterWave();
        }

        //The Update() method is run on every frame of the game.
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                var run = RoR2.Run.instance as RoR2.InfiniteTowerRun;
                var controller = run.waveController;
                BasicPickupDropTable table = controller.rewardDropTable as BasicPickupDropTable;
                Log.LogInfo(table);
                Log.LogInfo(table.GetType());

                Log.LogInfo("tier1Weight = " + table.tier1Weight);
                Log.LogInfo("tier2Weight = " + table.tier2Weight);
                Log.LogInfo("tier3Weight = " + table.tier3Weight);
                Log.LogInfo("bossWeight = " + table.bossWeight);
                Log.LogInfo("lunarEquipmentWeight = " + table.lunarEquipmentWeight);
                Log.LogInfo("lunarItemWeight = " + table.lunarItemWeight);
                Log.LogInfo("lunarCombinedWeight = " + table.lunarCombinedWeight);
                Log.LogInfo("equipmentWeight = " + table.equipmentWeight);
                Log.LogInfo("voidTier1Weight = " + table.voidTier1Weight);
                Log.LogInfo("voidTier2Weight = " + table.voidTier2Weight);
                Log.LogInfo("voidTier3Weight = " + table.voidTier3Weight);
                Log.LogInfo("voidBossWeight = " + table.voidBossWeight);

                Log.LogInfo("rewardOptionCount = " + controller.rewardOptionCount);
                Log.LogInfo("rewardDisplayTier = " + controller.rewardDisplayTier);

                // Doesn't work -- not sure if `table` is a reference or what?
                table.tier1Weight = 0;
                table.tier2Weight = 0;
                table.tier3Weight = 90;

                Log.LogInfo("[1] ref: " + table.GetInstanceID() + " " + table.GetHashCode());
                Log.LogInfo("[2] ref: " + (RoR2.Run.instance as RoR2.InfiniteTowerRun).waveController.rewardDropTable.GetInstanceID() +
                    " " + (RoR2.Run.instance as RoR2.InfiniteTowerRun).waveController.rewardDropTable.GetHashCode());

                controller.DropRewards();
            }
        }
    }
}
