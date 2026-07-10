namespace ConsolePlot.Plotting
{
    // A value type: many ticks are generated per draw (up to 2× per axis on the auto path), so a struct keeps them
    // off the GC heap — a List<Tick> stores them inline. Only each tick's Label string still allocates.
    internal readonly struct Tick
    {
        public double Value { get; }
        public string Label { get; }

        public Tick(double value, string label)
        {
            Value = value;
            Label = label;
        }
    }
}