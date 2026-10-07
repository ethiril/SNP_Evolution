namespace SnpEvolution.Cli
{
    // One --name value option a command takes. Commands that take the same option share its declaration.
    internal abstract class Option
    {
        protected Option(string name, string help)
        {
            Name = name;
            Help = help;
        }

        public string Name { get; }

        public string Help { get; }

        public string Flag => "--" + Name;

        public abstract string Placeholder { get; }

        public string Usage => $"{Flag} {Placeholder}";

        // The value the text gives, or false with why it gives none.
        public abstract bool TryRead(string text, out object? value, out string problem);
    }

    internal sealed class Option<T> : Option where T : notnull
    {
        public Option(string name, ValueKind<T> kind, string help, string? placeholder = null) : base(name, help)
        {
            Kind = kind;
            Placeholder = placeholder ?? kind.Placeholder;
        }

        public ValueKind<T> Kind { get; }

        public override string Placeholder { get; }

        public override bool TryRead(string text, out object? value, out string problem)
        {
            bool read = Kind.Parse(text, out T parsed);
            value = read ? parsed : null;
            problem = read ? "" : $"{Flag} takes {Kind.Expected}, not '{text}'.";
            return read;
        }
    }
}
