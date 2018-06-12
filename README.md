# SaveGamePatches

Extends the RimWorld modding patch system to include save game patching as well. Using the same syntax as standard Def patching, xml files are read from each mod's SaveGamePatches/ sub-directory and then applies to any saved game upon loaded. These patches are performed in transit and do not alter the actual saved file.

Note: Mod load order for this mod should not matter.

# Patching Advice
1. Delete the `<curJob>` node containing any JobDefs that are no longer present. If `pawn.curJob.def` is null, you will have a crash at some point.
2. Remove any things based on ThingClasses that are no longer present, or transition them into a ThingClass that does.
