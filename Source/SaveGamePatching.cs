using System.Xml;
using System.Linq;
using System.Collections.Generic;
using Harmony;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using Verse;

namespace RimWorld
{
	public static class SaveGamePatches
	{
		public static List<PatchOperation> patches;
		public static List<PatchOperation> notPresentPatches;
	}

	[StaticConstructorOnStartup]
	public class HarmonyPatches
	{
		static HarmonyPatches ()
		{
			SaveGamePatches.patches = new List<PatchOperation> ();
			SaveGamePatches.notPresentPatches = new List<PatchOperation>();
			var harmony = HarmonyInstance.Create("saveGamePatching");

			harmony.Patch(AccessTools.Method(typeof(Verse.SavedGameLoaderNow)
												, nameof(Verse.SavedGameLoaderNow.LoadGameFromSaveFileNow))
							, null, null, new HarmonyMethod(AccessTools.Method(typeof(LoadGameFromSaveFilePatch)
																, nameof(LoadGameFromSaveFilePatch.Transpiler))));
		}
	}

	static class LoadGameFromSaveFilePatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo scribeMetaLoad = AccessTools.Method(typeof(ScribeMetaHeaderUtility)
                                                            , nameof(ScribeMetaHeaderUtility.LoadGameDataHeader));
			MethodInfo applySaveGamePatches = AccessTools.Method(typeof(LoadGameFromSaveFilePatch)
																	, nameof(LoadGameFromSaveFilePatch.ApplySaveGamePatches));

			foreach(var code in instructions) {
				yield return code;
				if(code.operand == scribeMetaLoad)
					yield return new CodeInstruction(OpCodes.Call, applySaveGamePatches);
			}
		}

		private static void ApplySaveGamePatches()
		{
            DeepProfiler.Start ("Patching Save Game");
			LoadGameFromSaveFilePatch.LoadSaveGamePatches ();
            LoadGameFromSaveFilePatch.LoadNotPresentPatches ();
			Log.Message (string.Format ("Applying {0:d} SaveGamePatches", SaveGamePatches.patches.Count));
			foreach (PatchOperation patch in SaveGamePatches.patches) 
				patch.Apply (Scribe.loader.curXmlParent.OwnerDocument);
			Log.Message(string.Format("Applying {0:d} NotPresent Patches", SaveGamePatches.notPresentPatches.Count));  
            foreach (PatchOperation patch in SaveGamePatches.notPresentPatches) 
                patch.Apply (Scribe.loader.curXmlParent.OwnerDocument);
            DeepProfiler.End ();
		}

		public static void LoadSaveGamePatches()
		{
			SaveGamePatches.patches.Clear ();

			foreach(ModContentPack mod in LoadedModManager.RunningModsListForReading
				.Where(m => !ScribeMetaHeaderUtility.loadedModIdsList.Contains(m.Identifier)))
				LoadSaveGamePatchesFor(mod);
		}
        
        public static void LoadNotPresentPatches()
        {
            SaveGamePatches.notPresentPatches.Clear ();
            //LoadedModManager only has Ids ...
            foreach (ModMetaData mod in ScribeMetaHeaderUtility.loadedModIdsList
                           .Select(id => ModLister.AllInstalledMods.FirstOrDefault(mod => mod.Identifier == id))
                           .Where(mod => mod != null && !LoadedModManager.RunningMods
                           .Any(modContent => modContent.Name == mod.Name 
                                    || modContent.Name.Contains(mod.Name) 
                                    || mod.Name.Contains(modContent.Name))))
                LoadNotPresentPatchesFor (mod);
        }

		private static void LoadSaveGamePatchesFor(ModContentPack mod)	//Taken from Verse.ModContentPack.LoadPatches()
		{
			List<LoadableXmlAsset> list = DirectXmlLoader.XmlAssetsInModFolder (mod, "SaveGamePatches/").ToList<LoadableXmlAsset> ();
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
								SaveGamePatches.patches.Add(patchOperation);
							}
						}
					}
				} 
			}	
		}

		private static void LoadNotPresentPatchesFor(ModMetaData modMetaData)  //Adapted from Verse.ModContentPack.LoadPatches()
		{
			ModContentPack mod = new ModContentPack(modMetaData.RootDir, -1, "Loadable");
			List < LoadableXmlAsset > list = DirectXmlLoader.XmlAssetsInModFolder(mod, "NotPresentPatches/").ToList<LoadableXmlAsset>();
			for(int i = 0; i < list.Count; i++) {
				XmlElement documentElement = list[i].xmlDoc.DocumentElement;
				if(documentElement.Name != "Patch") {
					Log.Error(string.Format("Unexpected document element in patch XML; got {0}, expected 'Patch'", documentElement.Name));
				}
				else {
					for(int j = 0; j < documentElement.ChildNodes.Count; j++) {
						XmlNode xmlNode = documentElement.ChildNodes[j];
						if(xmlNode.NodeType == XmlNodeType.Element) {
							if(xmlNode.Name != "Operation") {
								Log.Error(string.Format("Unexpected element in patch XML; got {0}, expected 'Operation'", documentElement.ChildNodes[j].Name));
							}
							else {
								PatchOperation patchOperation = DirectXmlToObject.ObjectFromXml<PatchOperation>(xmlNode, false);
								patchOperation.sourceFile = list[i].FullFilePath;
								SaveGamePatches.notPresentPatches.Add(patchOperation);
							}
						}
					}
				}
			}
        }
	}	
}