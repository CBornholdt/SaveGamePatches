﻿using System.Xml;
using System.Linq;
using System.Collections.Generic;
using Harmony;
using System.Reflection;
using RimWorld;
using Verse;

namespace RimWorld
{
	public static class SaveGamePatches
	{
		public static List<PatchOperation> patches;
	}

	[StaticConstructorOnStartup]
	public class HarmonyPatches
	{
		static HarmonyPatches ()
		{
			SaveGamePatches.patches = new List<PatchOperation> ();
			var harmony = HarmonyInstance.Create("saveGamePatching");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			LoadGameFromSaveFilePatch.LoadSGPatches ();
		}
	}

	//Due to multithreading had issues Prefixing a non-static method ... choose to replace a static method instead
	[HarmonyPatch(typeof(Verse.SavedGameLoader))]
	[HarmonyPatch("LoadGameFromSaveFile")]
	static class LoadGameFromSaveFilePatch
	{
		public static bool Prefix(string fileName)
		{
			string str = GenText.ToCommaList (from mod in LoadedModManager.RunningMods
				select mod.ToString (), true);
			Log.Message ("Loading game from file " + fileName + " with mods " + str);
			DeepProfiler.Start ("Loading game from file " + fileName);
			Current.Game = new Game ();
			DeepProfiler.Start ("InitLoading (read file)");
			Scribe.loader.InitLoading (GenFilePaths.FilePathForSavedGame (fileName));
			DeepProfiler.End ();
			//BEGIN PATCH
			DeepProfiler.Start ("Patching Save Game");
			ApplySGPatches ();
			DeepProfiler.End ();
			//END PATCH
			ScribeMetaHeaderUtility.LoadGameDataHeader (ScribeMetaHeaderUtility.ScribeHeaderMode.Map, true);
			if (Scribe.EnterNode ("game")) {
				Current.Game = new Game ();
				Current.Game.LoadGame ();
				PermadeathModeUtility.CheckUpdatePermadeathModeUniqueNameOnGameLoad (fileName);
				DeepProfiler.End ();
				return false;	//Replace actual LoadGameFromSaveFile method
			}
			Log.Error ("Could not find game XML node.");
			Scribe.ForceStop ();

			return false;  //Replace actual LoadGameFromSaveFile method
		}

		private static void ApplySGPatches()
		{
			Log.Message (string.Format ("Applying {0:d} SGPatches", SaveGamePatches.patches.Count));
			foreach (PatchOperation patch in SaveGamePatches.patches) 
				patch.Apply (Scribe.loader.curXmlParent.OwnerDocument);
		}

		public static void LoadSGPatches()
		{
			foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
				LoadSGPatchesFor (mod);
		}

		private static void LoadSGPatchesFor(ModContentPack mod)	//Taken from Verse.ModContentPack.LoadPatches()
		{
			List<LoadableXmlAsset> list = DirectXmlLoader.XmlAssetsInModFolder (mod, "SGPatches/").ToList<LoadableXmlAsset> ();
			for (int i = 0; i < list.Count; i++) {
				XmlElement documentElement = list [i].xmlDoc.DocumentElement;
				if (documentElement.Name != "Patch") {
					Log.Error (string.Format ("Unexpected document element in patch XML; got {0}, expected 'Patch'", documentElement.Name));
				} else {
					for (int j = 0; j < documentElement.ChildNodes.Count; j++) {
						XmlNode xmlNode = documentElement.ChildNodes [j];
						if (xmlNode.NodeType == XmlNodeType.Element) {
							if (xmlNode.Name != "Operation") {
								Log.Error (string.Format ("Unexpected element in patch XML; got {0}, expected 'Operation'", documentElement.ChildNodes [j].Name));
							} else {
								PatchOperation patchOperation = DirectXmlToObject.ObjectFromXml<PatchOperation> (xmlNode, false);
								patchOperation.sourceFile = list [i].FullFilePath;
								SaveGamePatches.patches.Add (patchOperation);
							}
						}
					}
				} 
			}	
		}
	}	
}