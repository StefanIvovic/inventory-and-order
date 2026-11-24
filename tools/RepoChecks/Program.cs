using RepoChecks;
using RepoChecks.Images;
using RepoChecks.PackagePins;

return CommandLine.Run(args, Console.Out, Console.Error);

namespace RepoChecks
{
    public static class CommandLine
    {
        public const int Success = 0;
        public const int FindingsReported = 1;
        public const int UsageError = 2;

        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (args.Length == 0 || args[0] is not ("images" or "packages"))
            {
                error.WriteLine("Usage: RepoChecks <images|packages> [--root <path>]");
                return UsageError;
            }

            var root = Directory.GetCurrentDirectory();
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--root" && i + 1 < args.Length)
                {
                    root = args[++i];
                }
                else
                {
                    error.WriteLine($"Unknown argument: {args[i]}");
                    return UsageError;
                }
            }

            try
            {
                var fullRoot = Path.GetFullPath(root);
                var findings = args[0] == "images" ? ImageCheck.Run(fullRoot) : PinCheck.Run(fullRoot);
                foreach (var finding in findings)
                {
                    output.WriteLine(finding);
                }

                return findings.Count == 0 ? Success : FindingsReported;
            }
            catch (ConfigurationException exception)
            {
                error.WriteLine(exception.Message);
                return UsageError;
            }
        }
    }
}
