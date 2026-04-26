using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace SRTPluginProviderSH3C
{
    public static class GameHashes
    {
        // SHA256 hash of sh3.exe (Silent Hill 3 PC — official speedrun exe)
        // Hash: 7B97B8AEBEDD24532132CB3E6E68F1C093F295FC27B0BF9679963123AA95688B
        private static readonly byte[] sh3pc = new byte[32]
        {
            0x7B, 0x97, 0xB8, 0xAE, 0xBE, 0xDD, 0x24, 0x53,
            0x21, 0x32, 0xCB, 0x3E, 0x6E, 0x68, 0xF1, 0xC0,
            0x93, 0xF2, 0x95, 0xFC, 0x27, 0xB0, 0xBF, 0x96,
            0x79, 0x96, 0x31, 0x23, 0xAA, 0x95, 0x68, 0x8B
        };

        public static GameVersion DetectVersion(string filePath)
        {
            byte[] checksum;
            using (SHA256 hashFunc = SHA256.Create())
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                checksum = hashFunc.ComputeHash(fs);

            if (checksum.SequenceEqual(sh3pc))
                return GameVersion.sh3pc;
            else
                return GameVersion.Unknown;
        }
    }
}
