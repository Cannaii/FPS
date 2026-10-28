using System;
using System.IO;
using System.Reflection;
using AFPS.NetCode.Replay;

internal static class AuthorityTraceReplayProgram
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("用法：AuthorityTraceReplay <Host轨迹.afpt>...");
            }
            foreach (var reference in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
            {
                if (reference.Name.StartsWith("Unity", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("轨迹重放程序不能引用Unity程序集。");
                }
            }
            foreach (string path in args)
            {
                using (var stream = File.OpenRead(path))
                {
                    var result = AuthoritativeTraceReplay.Run(stream);
                    Console.WriteLine($"EngineFree=True;Trace={Path.GetFileName(path)};Entity={result.EntityId};"
                        + $"Frames={result.Frames};Resets={result.Resets};Algorithm={result.Identity.AlgorithmVersion};"
                        + $"World={result.Identity.WorldHash};Configuration={result.Identity.ConfigurationHash};"
                        + $"RollingRawHash={result.RollingHash};FinalRawHash={result.FinalHash}");
                }
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
