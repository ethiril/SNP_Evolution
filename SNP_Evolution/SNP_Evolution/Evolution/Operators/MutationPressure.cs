namespace SnpEvolution.Evolution.Operators
{
    // How hard mutation pushes, shared between an algorithm's mutation and whatever watches its progress. At zero
    // mutation behaves as configured; each extra edit makes every child at least that many more edits away from its
    // parent, which is how a stalled search is shaken loose.
    public sealed class MutationPressure
    {
        public int ExtraEdits { get; set; }
    }
}
