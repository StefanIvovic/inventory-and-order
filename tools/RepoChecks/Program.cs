using RepoChecks;
using RepoChecks.Images;

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
            if (args.Length == 0 || args[0] != "images")
            {
                error.WriteLine("Usage: RepoChecks images [--root <path>]");
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
                var findings = ImageCheck.Run(Path.GetFullPath(root));
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
