using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mono.Unix;
using SS14.Watchdog.Components.ServerManagement;
using SS14.Watchdog.Configuration.Updates;

namespace SS14.Watchdog.Components.Updates;

public class UpdateProviderLocalWithBuild : UpdateProvider
{
    private readonly IServerInstance _serverInstance;
    private readonly UpdateProviderLocalConfiguration _specificConfiguration;
    private readonly ILogger<UpdateProviderLocalWithBuild> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _repoPath;


    public UpdateProviderLocalWithBuild(IServerInstance serverInstance,
           UpdateProviderLocalConfiguration specificConfiguration,
           ILogger<UpdateProviderLocalWithBuild> logger,
           IConfiguration configuration)
    {
        _serverInstance = serverInstance;
        _specificConfiguration = specificConfiguration;
        _logger = logger;
        _configuration = configuration;
        _repoPath = Path.Combine(_serverInstance.InstanceDir, "source");
    }

    public override Task<bool> CheckForUpdateAsync(string? currentVersion, CancellationToken cancel = default)
    {
        return Task.FromResult(currentVersion != _specificConfiguration.CurrentVersion);
    }

    public override async Task<string?> RunUpdateAsync(string? currentVersion, string binPath,
        CancellationToken cancel = default)
    {
        try
        {
            if (currentVersion == _specificConfiguration.CurrentVersion)
            {
                return null;
            }

            var serverPlatform = GetHostSS14RID();
            var serverPackage = Path.Combine(_repoPath, "release", ServerZipName);

            await UpdateProviderGit.CommandHelperChecked("Failed to dotnet restore", _repoPath, "dotnet", ["restore"], cancel);

            await UpdateProviderGit.CommandHelperChecked("Failed to build Content Packaging",
                _repoPath, "dotnet", ["build", "Content.Packaging", "--configuration", "Release", "--no-restore", "/m"], cancel);

            await UpdateProviderGit.CommandHelperChecked("Failed to build Hybrid ACZ package with Content Packaging",
                        _repoPath, "dotnet", new[] { "run", "--project", "Content.Packaging", "server", "--platform", serverPlatform, "--hybrid-acz" }, cancel);

            _logger.LogTrace("Applying server update.");

            if (Directory.Exists(binPath))
            {
                Directory.Delete(binPath, true);
            }

            Directory.CreateDirectory(binPath);

            _logger.LogTrace("Extracting zip file");

            // Actually extract.
            await using (var stream = File.Open(serverPackage, FileMode.Open))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    archive.ExtractToDirectory(binPath);
                }
            }

            // Remove the package now that it's extracted.
            File.Delete(serverPackage);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // chmod +x Robust.Server

                var rsPath = Path.Combine(binPath, "Robust.Server");
                if (File.Exists(rsPath))
                {
                    var f = new UnixFileInfo(rsPath);
                    f.FileAccessPermissions |=
                        FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute |
                        FileAccessPermissions.OtherExecute;
                }
            }

            // ReSharper disable once RedundantTypeArgumentsOfMethod
            return _specificConfiguration.CurrentVersion;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to run update!");

            return null;
        }
    }
}
