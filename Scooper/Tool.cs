using System.Diagnostics;

namespace Scooper
{
    class Tool
    {
        public static Process Process(string command) {
            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd.exe",
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            Process process = new();
            process.StartInfo = startInfo;
            process.Start();
            process.StandardInput.AutoFlush = true;
            return process;
        }

        public static string Cmd(string command)
        {
            using Process process = Process(command);
            string res = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return res;
        }
    }
}
