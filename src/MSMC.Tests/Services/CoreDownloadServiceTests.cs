using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;
using io.NET.ZTR_OS.Features.CoreDownloader.Services;

namespace io.NET.ZTR_OS.Tests.Services;

public class CoreDownloadServiceTests
{
    #region Fake Helpers

    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        public List<HttpRequestMessage> CapturedRequests { get; } = [];

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            return await _handler(request, cancellationToken);
        }
    }

    public class FakeSource : ICoreDownloadSource
    {
        public string Name { get; set; } = "FakeSource";
        public int Priority { get; set; } = 1;
        public string? ForCountryHint { get; set; }

        public Func<CancellationToken, Task<bool>>? ProbeFunc { get; set; }
        public Func<string, CancellationToken, Task<List<string>>>? ListVersionsFunc { get; set; }
        public Func<string, string, CancellationToken, Task<ServerCorePackage?>>? ResolveFunc { get; set; }
        public Func<ServerCorePackage, string, string?, IProgress<(long, long)>?, CancellationToken, Task<CoreDownloadResult>>? DownloadFunc { get; set; }

        public int ProbeLatencyMs { get; set; } = 50;
        public bool ProbeAlive { get; set; } = true;

        public async Task<bool> ProbeAvailableAsync(CancellationToken ct = default)
        {
            if (ProbeFunc != null) return await ProbeFunc(ct);
            await Task.Delay(ProbeLatencyMs, ct);
            return ProbeAlive;
        }

        public Task<List<string>> ListVersionsAsync(string coreType, CancellationToken ct = default)
            => ListVersionsFunc != null ? ListVersionsFunc(coreType, ct) : Task.FromResult(new List<string>());

        public Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default)
            => ResolveFunc != null ? ResolveFunc(coreType, version, ct) : Task.FromResult<ServerCorePackage?>(null);

        public Task<CoreDownloadResult> DownloadAsync(ServerCorePackage pkg, string destDir,
            string? destFileName = null, IProgress<(long Downloaded, long Total)>? progress = null,
            CancellationToken ct = default)
            => DownloadFunc != null ? DownloadFunc(pkg, destDir, destFileName, progress, ct)
                : Task.FromResult(new CoreDownloadResult(CoreDownloadStatus.Failed, ErrorMessage: "No download func"));
    }

    private static ServerCorePackage MakePkg(string coreType = "paper", string version = "1.21.1",
        long size = 10_000_000, string? hash = null, string source = "Fake",
        string url = "https://example.com/test.jar")
        => new(coreType, version, size, hash, source, new Uri(url));

    private static byte[] MakeFakeJarBytes(int size = 10)
    {
        var b = new byte[size];
        for (int i = 0; i < size; i++) b[i] = (byte)(i * 13 % 256);
        return b;
    }

    private static string ComputeSha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var h in hash) sb.Append(h.ToString("x2"));
        return sb.ToString();
    }

    private static string ComputeSha1Hex(byte[] data)
    {
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var h in hash) sb.Append(h.ToString("x2"));
        return sb.ToString();
    }

    #endregion

    [Fact]
    public async Task ProbeAndRank_3Sources_OrdersByLatency_FiltersDead()
    {
        var dead = new FakeSource { Name = "Dead", ProbeAlive = false, ProbeLatencyMs = 10 };
        var fast = new FakeSource { Name = "Fast", ProbeAlive = true, ProbeLatencyMs = 80 };
        var slow = new FakeSource { Name = "Slow", ProbeAlive = true, ProbeLatencyMs = 120 };

        var svc = new CoreDownloadService([dead, fast, slow]);

        var ranked = await svc.ProbeAndRankSourcesAsync();

        Assert.Equal(2, ranked.Count);
        Assert.Equal("Fast", ranked[0].Name);
        Assert.Equal("Slow", ranked[1].Name);
        Assert.DoesNotContain(ranked, s => s.Name == "Dead");
    }

    [Fact]
    public async Task DownloadSmart_Source1Fails_SeamlessFailoverToSource2()
    {
        var pkg = MakePkg();

        var src1 = new FakeSource
        {
            Name = "Src1",
            ResolveFunc = (_, _, _) => Task.FromResult<ServerCorePackage?>(pkg with { SourceName = "Src1" }),
            DownloadFunc = (_, _, _, _, _) =>
                throw new HttpRequestException("Src1 network down")
        };

        string? actualDestFile = null;
        var src2 = new FakeSource
        {
            Name = "Src2",
            ResolveFunc = (_, _, _) => Task.FromResult<ServerCorePackage?>(pkg with { SourceName = "Src2" }),
            DownloadFunc = (p, dest, name, _, _) =>
            {
                var fileName = name ?? $"{p.CoreType}-{p.Version}.jar";
                var full = Path.Combine(dest, fileName);
                Directory.CreateDirectory(dest);
                File.WriteAllBytes(full, MakeFakeJarBytes(100));
                actualDestFile = full;
                return Task.FromResult(new CoreDownloadResult(CoreDownloadStatus.Completed, full, 100, 100, ElapsedMs: 10, HashVerified: true));
            }
        };

        using var tmp = new TempDir();
        var svc = new CoreDownloadService([src1, src2]);

        var result = await svc.DownloadSmartAsync("paper", "1.21.1", tmp.Dir);

        Assert.Equal(CoreDownloadStatus.Completed, result.Status);
        Assert.NotNull(result.SavedFilePath);
        Assert.True(File.Exists(result.SavedFilePath));
    }

    [Fact]
    public void VerifySha256_ValidHash_ReturnsTrue()
    {
        var data = MakeFakeJarBytes(10);
        var expectedHash = ComputeSha256Hex(data);

        var ok = CoreDownloadService.VerifySha256(data, expectedHash);

        Assert.True(ok);
    }

    [Fact]
    public void VerifySha256_InvalidHash_ReturnsFalse()
    {
        var data = MakeFakeJarBytes(10);

        var ok = CoreDownloadService.VerifySha256(data, "0000000000000000000000000000000000000000000000000000000000000000");

        Assert.False(ok);
    }

    [Fact]
    public async Task HashMismatch_AutoRetryTwoMoreSources()
    {
        var goodData = MakeFakeJarBytes(10);
        var badData = new byte[10];
        for (int i = 0; i < 10; i++) badData[i] = (byte)(i + 1);
        var expectedSha256 = ComputeSha256Hex(goodData);
        var basePkg = MakePkg(hash: expectedSha256);

        var src1Tried = false;
        var src2Tried = false;

        var src1 = new FakeSource
        {
            Name = "Src1-Bad",
            ResolveFunc = (_, _, _) => Task.FromResult<ServerCorePackage?>(basePkg with { SourceName = "Src1-Bad" }),
            DownloadFunc = (p, dest, name, _, _) =>
            {
                src1Tried = true;
                var fileName = name ?? $"{p.CoreType}-{p.Version}.jar";
                var full = Path.Combine(dest, fileName);
                Directory.CreateDirectory(dest);
                File.WriteAllBytes(full, badData);
                return Task.FromResult(new CoreDownloadResult(CoreDownloadStatus.Completed, full, badData.Length, p.SizeBytes, ElapsedMs: 5));
            }
        };

        var src2 = new FakeSource
        {
            Name = "Src2-Bad",
            ResolveFunc = (_, _, _) => Task.FromResult<ServerCorePackage?>(basePkg with { SourceName = "Src2-Bad" }),
            DownloadFunc = (p, dest, name, _, _) =>
            {
                src2Tried = true;
                var fileName = name ?? $"{p.CoreType}-{p.Version}.jar";
                var full = Path.Combine(dest, fileName);
                Directory.CreateDirectory(dest);
                File.WriteAllBytes(full, badData);
                return Task.FromResult(new CoreDownloadResult(CoreDownloadStatus.Completed, full, badData.Length, p.SizeBytes, ElapsedMs: 5));
            }
        };

        var src3 = new FakeSource
        {
            Name = "Src3-Good",
            ResolveFunc = (_, _, _) => Task.FromResult<ServerCorePackage?>(basePkg with { SourceName = "Src3-Good" }),
            DownloadFunc = (p, dest, name, _, _) =>
            {
                var fileName = name ?? $"{p.CoreType}-{p.Version}.jar";
                var full = Path.Combine(dest, fileName);
                Directory.CreateDirectory(dest);
                File.WriteAllBytes(full, goodData);
                return Task.FromResult(new CoreDownloadResult(CoreDownloadStatus.Completed, full, goodData.Length, p.SizeBytes, ElapsedMs: 5));
            }
        };

        using var tmp = new TempDir();
        var svc = new CoreDownloadService([src1, src2, src3]);

        var result = await svc.DownloadSmartAsync("paper", "1.21.1", tmp.Dir);

        Assert.True(src1Tried, "src1 should have been tried first");
        Assert.True(src2Tried, "src2 should have been tried after src1 hash mismatch");
        Assert.Equal(CoreDownloadStatus.Completed, result.Status);
        Assert.True(result.HashVerified);
    }

    [Fact]
    public async Task ResumeDownloadWithRangeHeader_SourceSupportsRange()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(async (req, ct) =>
        {
            capturedRequest = req;
            var fullBytes = new byte[2_097_152];
            for (int i = 0; i < fullBytes.Length; i++) fullBytes[i] = (byte)(i % 256);

            if (req.Headers.Range != null && req.Headers.Range.Ranges.Count > 0)
            {
                var range = req.Headers.Range.Ranges.First();
                var from = range.From ?? 0;
                var to = range.To ?? fullBytes.Length - 1;
                var length = to - from + 1;
                var slice = new byte[length];
                Array.Copy(fullBytes, from, slice, 0, length);
                var resp = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(slice)
                };
                resp.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, to, fullBytes.Length);
                return resp;
            }
            else
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fullBytes)
                };
                resp.Content.Headers.ContentLength = fullBytes.Length;
                resp.Content.Headers.AcceptRanges.Add("bytes");
                return resp;
            }
        });

        using var tmp = new TempDir();
        var destFile = Path.Combine(tmp.Dir, "paper-1.21.1.jar");
        Directory.CreateDirectory(tmp.Dir);

        var firstMB = new byte[1_048_576];
        for (int i = 0; i < firstMB.Length; i++) firstMB[i] = (byte)(i % 256);
        File.WriteAllBytes(destFile, firstMB);

        var pkg = MakePkg(size: 2_097_152, url: "https://example.com/paper.jar");

        var src = new FakeSource
        {
            Name = "ResumeSrc",
            ResolveFunc = (_, _, _) => Task.FromResult<ServerCorePackage?>(pkg),
            DownloadFunc = async (p, dest, name, progress, ct) =>
            {
                var fileName = name ?? $"{p.CoreType}-{p.Version}.jar";
                var full = Path.Combine(dest, fileName);
                long already = 0;
                if (File.Exists(full))
                    already = new FileInfo(full).Length;

                using var httpClient = new HttpClient(handler);
                using var reqMsg = new HttpRequestMessage(HttpMethod.Get, p.DownloadUrl);
                if (already > 0)
                    reqMsg.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(already, null);

                using var resp = await httpClient.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                var total = resp.Content.Headers.ContentRange?.Length ?? p.SizeBytes;
                long downloaded = already;

                await using var srcStream = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.None);
                var buf = new byte[64 * 1024];
                int n;
                while ((n = await srcStream.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                {
                    await dst.WriteAsync(buf, 0, n, ct);
                    downloaded += n;
                    progress?.Report((downloaded, total));
                }

                return new CoreDownloadResult(CoreDownloadStatus.Completed, full, downloaded, total, ElapsedMs: 10);
            }
        };

        var svc = new CoreDownloadService([src]);
        var result = await svc.DownloadSmartAsync("paper", "1.21.1", tmp.Dir, destFileName: "paper-1.21.1.jar");

        Assert.NotNull(capturedRequest, "HTTP request should have been made");
        Assert.NotNull(capturedRequest!.Headers.Range, "Range header should be set");
        var rangeItem = Assert.Single(capturedRequest.Headers.Range.Ranges);
        Assert.Equal(1_048_576, rangeItem.From);
        Assert.Equal(2_097_152, result.DownloadedBytes);
        Assert.Equal(CoreDownloadStatus.Completed, result.Status);
    }

    private sealed class TempDir : IDisposable
    {
        public string Dir { get; }
        public TempDir() => Dir = Path.Combine(Path.GetTempPath(), $"msmc_test_{Guid.NewGuid():N}");
        public void Dispose()
        {
            if (Directory.Exists(Dir))
            {
                try { Directory.Delete(Dir, true); } catch { }
            }
        }
    }
}
