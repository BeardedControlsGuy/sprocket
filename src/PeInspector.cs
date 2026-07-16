using System.IO;

namespace Sprocket
{
    /// <summary>Reads the PE header of a native exe to tell x86 from x64, without running it.</summary>
    internal static class PeInspector
    {
        private const ushort IMAGE_FILE_MACHINE_AMD64 = 0x8664;
        private const ushort IMAGE_FILE_MACHINE_ARM64 = 0xAA64;

        public static bool IsX64(string exePath)
        {
            try
            {
                using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    int peOffset = br.ReadInt32();

                    fs.Seek(peOffset, SeekOrigin.Begin);
                    uint peSignature = br.ReadUInt32();
                    if (peSignature != 0x00004550) return false; // "PE\0\0"

                    ushort machine = br.ReadUInt16();
                    return machine == IMAGE_FILE_MACHINE_AMD64 || machine == IMAGE_FILE_MACHINE_ARM64;
                }
            }
            catch
            {
                return true; // Niagara 4.15 hasn't shipped 32-bit in years; assume 64 if unreadable.
            }
        }
    }
}
