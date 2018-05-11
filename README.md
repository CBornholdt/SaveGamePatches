# SaveGamePatches

Extends the RimWorld modding patch system to include save game patching as well. Using the same syntax as standard Def patching, xml files are read from each mod's SaveGamePatches/ sub-directory and then applies to any saved game upon loaded. These patches are performed in transit and do not alter the actual saved file.

Note: Mod load order for this mod should not matter.
