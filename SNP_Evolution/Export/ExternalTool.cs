using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SnpEvolution.Export
{
    // Runs a program the co-simulations need, which may not be installed.
    public static class ExternalTool
    {
        // The full path of a program on PATH, or null when it is not there.
        public static string? Find(string program)
        {
            string[] folders = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            // Homebrew's folders too, since a run started from an IDE may not have them on PATH.
            return folders.Append("/opt/homebrew/bin").Append("/usr/local/bin").Select(folder => Path.Combine(folder, program)).FirstOrDefault(File.Exists);
        }

        // Standard output; throws with standard error when the program fails.
        public static string Run(string program, string arguments, string workingFolder)
        {
            var start = new ProcessStartInfo(program, arguments)
            {
                WorkingDirectory = workingFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {program}.");
            var error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{Path.GetFileName(program)} {arguments} failed with exit code {process.ExitCode}: {error.Result}{output}");
            }
            return output;
        }
    }

    // Compiles a design and its testbench with Icarus Verilog and returns what the testbench prints.
    public static class Iverilog
    {
        public const string Missing = "Icarus Verilog (iverilog) is not installed; on macOS, brew install icarus-verilog.";

        public static bool IsInstalled => ExternalTool.Find("iverilog") != null && ExternalTool.Find("vvp") != null;

        public static string Simulate(VerilogDesign design, string testbench, string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, design.Name + ".v"), design.Module);
            File.WriteAllText(Path.Combine(folder, design.Name + "_tb.v"), testbench);
            string iverilog = ExternalTool.Find("iverilog") ?? throw new InvalidOperationException(Missing);
            string vvp = ExternalTool.Find("vvp") ?? throw new InvalidOperationException(Missing);
            ExternalTool.Run(iverilog, $"-g2005 -Wall -o {design.Name}.vvp {design.Name}_tb.v {design.Name}.v", folder);
            string output = ExternalTool.Run(vvp, $"-n {design.Name}.vvp", folder);
            // vvp reports where $finish was called; the rest is the testbench's own output.
            return string.Concat(output.Split('\n').Where(line => line.Length > 0 && !line.Contains("$finish")).Select(line => line + "\n"));
        }
    }

    // Uppaal's command-line model checker, found on PATH, at UPPAAL_VERIFYTA, or inside an Uppaal folder in /Applications.
    public static class Verifyta
    {
        public const string Missing = "Uppaal's verifyta is not installed; get UPPAAL from uppaal.org (free academic licence), then put verifyta on PATH or set UPPAAL_VERIFYTA.";

        public static string? Program
        {
            get
            {
                if (Environment.GetEnvironmentVariable("UPPAAL_VERIFYTA") is string set && File.Exists(set))
                {
                    return set;
                }
                if (ExternalTool.Find("verifyta") is string onPath)
                {
                    return onPath;
                }
                return Directory.Exists("/Applications")
                    ? Directory.EnumerateDirectories("/Applications").Where(folder => Path.GetFileName(folder).Contains("uppaal", StringComparison.OrdinalIgnoreCase))
                        .SelectMany(folder => Directory.EnumerateFiles(folder, "verifyta", SearchOption.AllDirectories)).FirstOrDefault()
                    : null;
            }
        }

        public static bool IsInstalled => Program != null;

        // Whether each query holds, in the order of the .q file; throws when verifyta fails or reports fewer verdicts.
        public static IReadOnlyList<bool> Check(UppaalModel model, string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, model.Name + ".xml"), model.Model);
            File.WriteAllText(Path.Combine(folder, model.Name + ".q"), model.Queries);
            string program = Program ?? throw new InvalidOperationException(Missing);
            string output = ExternalTool.Run(program, $"-q \"{model.Name}.xml\" \"{model.Name}.q\"", folder);
            IReadOnlyList<bool> verdicts = Verdicts(output);
            if (verdicts.Count != model.QueryNames.Count)
            {
                throw new InvalidOperationException($"verifyta gave {verdicts.Count} verdicts for {model.QueryNames.Count} queries:\n{output}");
            }
            return verdicts;
        }

        // Whether each query holds, from verifyta's "Formula is satisfied" and "Formula is NOT satisfied" lines.
        public static IReadOnlyList<bool> Verdicts(string output) =>
            output.Split('\n').Where(line => line.Contains("Formula is")).Select(line => !line.Contains("NOT")).ToList();
    }
}
