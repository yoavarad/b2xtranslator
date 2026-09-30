using NUnit.Framework;

// Fixtures write to distinct files and share no static state, so they may run in parallel.
[assembly: Parallelizable(ParallelScope.Fixtures)]
