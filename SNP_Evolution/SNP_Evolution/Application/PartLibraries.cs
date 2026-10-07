using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Application
{
    // Reading part libraries and part files for every run, and saving what a composition run added to its library.
    internal static class PartLibraries
    {
        // The library a run with these settings builds from.
        public static Loaded<ModuleLibrary> Load(Settings settings, Action<string>? log = null) =>
            Load(settings.PartLibraryFolder, settings.HandBuiltParts, settings.HandBuiltAddLoop, log);

        // The saved parts in the folder, with the hand-built parts when asked for; a missing folder is an empty library.
        public static Loaded<ModuleLibrary> Load(string folder, bool handBuilt, bool addLoop, Action<string>? log = null) =>
            Loaded<ModuleLibrary>.Try(() => PartLibraryFiles.Load(folder, log)).Select(library =>
            {
                if (handBuilt)
                {
                    HandBuiltMachines.AddParts(library, log ?? (_ => { }), addLoop);
                }
                return library;
            });

        // One part file, with the path it was read from.
        public static Loaded<PartFile> ReadPart(string file) =>
            Loaded<PartFile>.Try(() => new PartFile(PartLibraryFiles.Read(File.ReadAllText(file), Path.GetFileName(file)), file));

        // Each part in the folder with the file it is saved to, which is the file it was loaded from.
        public static Loaded<IReadOnlyList<PartFile>> PartsIn(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return Loaded<IReadOnlyList<PartFile>>.Failed($"There is no part library folder '{folder}'.");
            }
            return Load(folder, handBuilt: false, addLoop: false).Select<IReadOnlyList<PartFile>>(library => library.Parts.Select(module => module.Part).OfType<LibraryPart>()
                .Select(part => new PartFile(part, Path.Combine(folder, PartLibraryFiles.FileName(part.Contract)))).ToList());
        }

        // The default folder holds only parts runs found, so a library with hand-built parts never goes there.
        public static void SaveIfGrown(Settings settings, ModuleLibrary? parts, int partsAtStart, Action<string> log)
        {
            if (parts == null || parts.Parts.Count == partsAtStart)
            {
                return;
            }
            if (settings.HandBuiltParts && Path.GetFullPath(settings.PartLibraryFolder) == Path.GetFullPath(Settings.DefaultPartLibraryFolder()))
            {
                log("The library holds hand-built parts, so it is not saved to the default part library folder; give another folder to keep it.");
                return;
            }
            IReadOnlyList<string> written = PartLibraryFiles.Save(parts, settings.PartLibraryFolder);
            log($"Saved {written.Count} part(s) to {settings.PartLibraryFolder}.");
        }
    }

    // A library part and the file it is saved in.
    internal sealed record PartFile(LibraryPart Part, string Path);
}
