using BenchmarkDotNet.Running;

namespace b2xtranslator.Benchmarks
{
    public static class Program
    {
        // No arguments: run everything instead of showing the interactive switcher menu.
        public static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args.Length == 0 ? new[] { "--filter", "*" } : args);
    }
}
