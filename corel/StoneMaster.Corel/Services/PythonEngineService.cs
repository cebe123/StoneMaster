using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var assemblyDir = Path.GetDirectoryName(typeof(PythonEngineService).Assembly.Location) ?? baseDir;
            var candidates = new[]
            {
                Path.Combine(baseDir, "StoneMaster.Engine", "StoneMaster.Engine.exe"),
                Path.Combine(assemblyDir, "StoneMaster.Engine", "StoneMaster.Engine.exe"),
                Path.Combine(assemblyDir, "StoneMaster.Engine.exe")
            };
            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        public async Task<EngineResponse> GenerateAsync(EngineRequest request, IProgress<string> progress, CancellationToken token)
        {
            if (!File.Exists(_engineExe))
                throw new FileNotFoundException("StoneMaster.Engine.exe bulunamadı.", _engineExe);
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.image_path) || !File.Exists(request.image_path))
                throw new FileNotFoundException("İşlenecek görsel bulunamadı.", request.image_path);

            var tempDir = CreateTempDirectory();
            var requestPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".json");
            var responsePath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".response.json");
            File.WriteAllText(requestPath, JsonService.Serialize(request), new UTF8Encoding(false));

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _engineExe,
                    Arguments = Quote(requestPath) + " " + Quote(responsePath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(_engineExe)
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += (_, e) => ReportProgressLine(progress, e.Data);
                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            progress?.Report("ENGINE ERROR: " + e.Data);
                    };

                    if (!process.Start())
                        throw new InvalidOperationException("StoneMaster.Engine.exe başlatılamadı.");
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await WaitForExitAsync(process, token).ConfigureAwait(false);

                    if (!File.Exists(responsePath))
                        throw new InvalidOperationException("Engine response üretmedi. ExitCode=" + process.ExitCode);

                    var responseJson = File.ReadAllText(responsePath, Encoding.UTF8);
                    var response = JsonService.Deserialize<EngineResponse>(responseJson);
                    if (response == null)
                        throw new InvalidOperationException("Engine boş response döndürdü.");
                    if (!response.success)
                        throw new InvalidOperationException(response.error ?? "Engine bilinmeyen bir hata döndürdü.");
                    return response;
                }
            }
            finally
            {
                TryDelete(requestPath);
                TryDelete(responsePath);
                TryDeleteDirectory(tempDir);
            }
        }

        public async Task<List<(string Name, string Hex, double Percentage)>> AnalyzeImageColorsAsync(string imagePath)
        {
            if (!File.Exists(_engineExe))
                throw new FileNotFoundException("StoneMaster.Engine.exe bulunamadı.", _engineExe);
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Görsel dosyası bulunamadı.", imagePath);

            var tempDir = CreateTempDirectory();
            var analysisPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".analysis.json");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _engineExe,
                    Arguments = "analyze-colors " + Quote(imagePath) + " " + Quote(analysisPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(_engineExe)
                };

                var output = new StringBuilder();
                using (var process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) output.AppendLine("ERROR: " + e.Data); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await WaitForExitAsync(process, CancellationToken.None).ConfigureAwait(false);

                    if (!File.Exists(analysisPath))
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(output.ToString())
                            ? "Renk analizi yapılamadı."
                            : output.ToString());

                    var json = File.ReadAllText(analysisPath, Encoding.UTF8);
                    var result = JsonService.Deserialize<ColorAnalysisResult>(json);
                    return (result?.colors ?? new List<ColorAnalysisItem>())
                        .Where(item => item != null)
                        .Select(item => (item.name ?? "Renk", item.hex ?? "#000000", item.percentage))
                        .ToList();
                }
            }
            finally
            {
                TryDelete(analysisPath);
                TryDeleteDirectory(tempDir);
            }
        }

        private static async Task WaitForExitAsync(Process process, CancellationToken token)
        {
            while (!process.HasExited)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoneMaster", "temp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
        private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
        private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

        private static void ReportProgressLine(IProgress<string> progress, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            const string prefix = "PROGRESS=";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var separator = line.IndexOf('|');
                progress?.Report(separator > 0 ? line.Substring(separator + 1) : line);
            }
            else
            {
                progress?.Report(line);
            }
        }
    }

    public sealed class ColorAnalysisResult
    {
        public List<ColorAnalysisItem> colors { get; set; } = new List<ColorAnalysisItem>();
        public int total_colors { get; set; }
    }

    public sealed class ColorAnalysisItem
    {
        public string name { get; set; }
        public string hex { get; set; }
        public double percentage { get; set; }
    }
}
