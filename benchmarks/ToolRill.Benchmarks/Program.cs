using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
var config = DefaultConfig.Instance.WithArtifactsPath(
    Path.Combine(projectDir, "BenchmarkDotNet.Artifacts"));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);