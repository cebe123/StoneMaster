using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StoneMaster.Corel.Models;

namespace StoneMaster.Corel.Services
{
    public sealed class PythonEngineService
    {
        private readonly string _engineExe;

        public PythonEngineService()
        {
            _engineExe = ResolveEnginePath();
        }

        private static string ResolveEnginePath()
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StoneMaster.Engine", "StoneMaster.Engine.exe"),
                Path.Combine(Path.GetDirectoryName(typeof(PythonEngineService).Assembly.Location), "StoneMaster.Engine", "StoneMaster.Engine.exe"),
                Path.Combine(Path.GetDirectoryName(typeof(PythonEngineService).Assembly.Location), "StoneMaster.Engine.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return candidates[0];
        }

        public async Task<EngineResponse> GenerateAsync(EngineRequest request, IProgress<string> progress, CancellationToken token)
        {
            if (!File.Exists(_engineExe))
                throw new FileNotFoundException("StoneMaster.Engine.exe bulunamadı.", _engineExe);

            var tempDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StoneMaster", "temp");
            Directory.CreateDirectory(tempDir);

            var requestPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".json");
            var responsePath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".response.json");

            File.WriteAllText(requestPath, JsonService.Serialize(request), new UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = _engineExe,
                Arguments = "\"" + requestPath + "\" \"" + responsePath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(_engineExe)
            };

            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) progress?.Report(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) progress?.Report("ENGINE ERROR: " + e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit(), token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                {
                    try { process.Kill(); } catch { }
                    token.ThrowIfCancellationRequested();
                }

                if (!File.Exists(responsePath))
                    throw new InvalidOperationException("Engine response üretmedi. ExitCode=" + process.ExitCode);

                var responseJson = File.ReadAllText(responsePath, Encoding.UTF8);
                return JsonService.Deserialize<EngineResponse>(responseJson);
            }
        }
    }
}