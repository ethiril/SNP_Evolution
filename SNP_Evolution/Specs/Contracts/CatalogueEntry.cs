using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Specs.Contracts
{
    // A catalogued contract: the specification it is built from and the values its cases test, each row a value per
    // data in-port in order.
    public sealed record CatalogueEntry(Specification Specification, IReadOnlyList<IReadOnlyList<int>> Values)
    {
        public Contract Contract { get; } = Specification.ContractFor(Values);

        public static CatalogueEntry Of(Specification specification, IEnumerable<IReadOnlyList<int>> values) => new CatalogueEntry(specification, values.ToList());

        // A specification with no data in-ports, which has one case.
        public static CatalogueEntry OneCase(Specification specification) => Of(specification, new[] { Array.Empty<int>() });
    }
}
