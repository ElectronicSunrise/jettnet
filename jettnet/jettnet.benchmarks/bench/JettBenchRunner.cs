// using System;
// using BenchmarkDotNet.Reports;
// using BenchmarkDotNet.Running;
//
// namespace jettnet.benchmarks.bench
// {
//     internal class JettBenchRunner
//     {
//         private static void Main(string[] args)
//         {
//             Console.WriteLine("Test starting!");
//
//             //var summaryByte = BenchmarkRunner.Run<ReadWriteByte>();
//             Summary summary = BenchmarkRunner.Run<ReadsAndWrites>();
//
//             Console.WriteLine("Summary: ");
//             Console.WriteLine(summary.AllRuntimes);
//
//             Console.ReadKey();
//         }
//     }
// }