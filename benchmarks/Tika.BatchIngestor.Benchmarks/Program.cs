using BenchmarkDotNet.Running;
using Tika.BatchIngestor.Benchmarks;

Console.WriteLine("==============================================");
Console.WriteLine("   Tika.BatchIngestor Performance Benchmarks  ");
Console.WriteLine("   IoT & Time-Series Data Ingestion Scenarios ");
Console.WriteLine("==============================================");
Console.WriteLine();
Console.WriteLine("Benchmark Scenarios:");
Console.WriteLine("  1. Small IoT Sensor Readings (~100 bytes/row)");
Console.WriteLine("  2. Medium Vehicle Telemetry (~500 bytes/row)");
Console.WriteLine("  3. Minimal Time-Series Metrics (~64 bytes/row)");
Console.WriteLine("  4. Large Industrial Machine Logs (~2KB/row)");
Console.WriteLine();
Console.WriteLine("Test Parameters:");
Console.WriteLine("  - Row Counts: 5,000 | 10,000 | 15,000");
Console.WriteLine("  - Batch Sizes: 500 | 1,000 | 2,000");
Console.WriteLine("  - Max Parallelism: 2 | 4 | 8");
Console.WriteLine();
Console.WriteLine("Starting benchmarks...");
Console.WriteLine();

var summary = BenchmarkRunner.Run<IoTBenchmarks>();

Console.WriteLine();
Console.WriteLine("Benchmarks completed!");
Console.WriteLine($"Results saved to: {summary.ResultsDirectoryPath}");
