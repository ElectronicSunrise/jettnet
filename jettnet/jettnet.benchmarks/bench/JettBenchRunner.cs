using BenchmarkDotNet.Running;
using System;

namespace jettnet.benchmarks.bench
{
    class JettBenchRunner
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Test starting!");

            //var summaryByte = BenchmarkRunner.Run<ReadWriteByte>();
            var summary = BenchmarkRunner.Run<ReadsAndWrites>();

            Console.WriteLine("Summary: ");
            Console.WriteLine(summary.AllRuntimes);

            Console.ReadKey();
        }
    }
}
