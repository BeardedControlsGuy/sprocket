using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Sprocket
{
    internal sealed class HeapSizes
    {
        public int StationMb;
        public int WorkbenchMb;
        public bool Found;
    }

    /// <summary>Reads/writes the -Xmx heap sizes in a platform's nre.properties (same file Albert tunes by hand).</summary>
    internal static class MemorySettings
    {
        private static readonly Regex XmxPattern = new Regex(@"-Xmx(\d+)([MmGg])");

        public static HeapSizes Read(NiagaraPlatform platform)
        {
            HeapSizes result = new HeapSizes();

            if (!File.Exists(platform.NreProperties)) return result;

            foreach (string line in File.ReadAllLines(platform.NreProperties))
            {
                if (line.StartsWith("station.java.options="))
                {
                    int mb = ParseXmxMb(line);
                    if (mb > 0) { result.StationMb = mb; result.Found = true; }
                }
                else if (line.StartsWith("wb.java.options="))
                {
                    int mb = ParseXmxMb(line);
                    if (mb > 0) { result.WorkbenchMb = mb; result.Found = true; }
                }
            }

            return result;
        }

        public static void Write(NiagaraPlatform platform, int stationMb, int wbMb)
        {
            string path = platform.NreProperties;
            string[] lines = File.ReadAllLines(path);

            File.Copy(path, path + ".bak", true);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("station.java.options="))
                    lines[i] = ReplaceXmx(lines[i], stationMb);
                else if (lines[i].StartsWith("wb.java.options="))
                    lines[i] = ReplaceXmx(lines[i], wbMb);
            }

            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        private static int ParseXmxMb(string line)
        {
            Match m = XmxPattern.Match(line);
            if (!m.Success) return -1;

            int value = int.Parse(m.Groups[1].Value);
            string unit = m.Groups[2].Value.ToUpperInvariant();
            return unit == "G" ? value * 1024 : value;
        }

        private static string ReplaceXmx(string line, int newMb)
        {
            return XmxPattern.Replace(line, "-Xmx" + newMb + "M");
        }
    }
}
