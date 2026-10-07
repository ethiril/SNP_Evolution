using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Search.Operators
{
    public sealed record WeightedEdit(string Name, IMutation Edit, double Weight);
}
