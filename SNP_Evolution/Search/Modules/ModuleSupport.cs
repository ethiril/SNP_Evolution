using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Search.Modules
{
    // The library and how modules are treated, for building the mutation. Tracker is set for the main run only, so
    // credit and harvesting follow the task the run is scored on.
    public sealed record ModuleSupport(ModuleLibrary Library, bool Freeze = true, ModuleTracker? Tracker = null);
}
