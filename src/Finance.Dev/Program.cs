namespace Finance.Dev;

using System.Diagnostics;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var repositoryRoot = GetRepositoryRoot();
        var apiProject = Path.Combine(repositoryRoot, "src", "Finance.Api", "Finance.Api.csproj");
        var apiArgs = new List<string>
        {
            "run",
            "--project",
            apiProject,
            "--urls",
            "http://localhost:5000"
        };

        apiArgs.AddRange(args);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        }.WithArguments(apiArgs));

        if (process is null)
        {
            Console.Error.WriteLine("Failed to start Finance.Api.");
            return 1;
        }

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        };

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Finance.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }

    private static ProcessStartInfo WithArguments(this ProcessStartInfo startInfo, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
