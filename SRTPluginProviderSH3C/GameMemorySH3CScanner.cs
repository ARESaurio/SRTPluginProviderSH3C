using SRTPluginProviderSH3C.Structs.GameStructs;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SRTPluginProviderSH3C
{
    public class GameMemorySH3CScanner : IDisposable
    {
        // ── Win32 P/Invoke ────────────────────────────────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, ulong lpBaseAddress,
            byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

        private const uint PROCESS_VM_READ           = 0x0010;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        // ── SH3 PC Memory Offsets ─────────────────────────────────────────────
        // All offsets are static from the game's base address.
        // Source: Silent Hill 3 NHT minimalist by Ares & Miguel_mm_95.

        // Player
        private const ulong OFFSET_PLAYER_HP     = 0x498668;
        private const ulong OFFSET_DAMAGE_TAKEN  = 0x498650;

        // In-Game Time & FPS
        private const ulong OFFSET_IGT           = 0x6CE66F4;
        private const ulong OFFSET_FPS           = 0x6D2CAB8;

        // Weapons
        private const ulong OFFSET_HANDGUN       = 0x6D2CAA2;
        private const ulong OFFSET_SHOTGUN       = 0x6D2CAA4;
        private const ulong OFFSET_MACHINEGUN    = 0x6D2CAA6;

        // Run Stats
        private const ulong OFFSET_SAVES         = 0x6CE66E4;
        private const ulong OFFSET_ITEMS         = 0x6CE66E8;
        private const ulong OFFSET_SHOOTING      = 0x6CE66EA;
        private const ulong OFFSET_MELEE         = 0x6CE66EC;

        // Difficulty
        private const ulong OFFSET_ACTION_DIFF   = 0x6CE66DE;
        private const ulong OFFSET_RIDDLE_DIFF   = 0x6CE66DF;

        // Boss Fight Times
        private const ulong OFFSET_WORM_TIME       = 0x6CE6704;
        private const ulong OFFSET_MISSIONARY_TIME = 0x6CE6708;
        private const ulong OFFSET_LEONARD_TIME    = 0x6CE670C;
        private const ulong OFFSET_ALESSA_TIME     = 0x6CE6710;
        private const ulong OFFSET_GOD_TIME        = 0x6CE6714;

        // ── State ─────────────────────────────────────────────────────────────
        private IntPtr        processHandle = IntPtr.Zero;
        private uint          processId;
        private ulong         baseAddress   = 0;
        private bool          processFound  = false;
        private GameMemorySH3C gameMemoryValues;

        public bool HasScanned    { get; private set; }
        public bool ProcessRunning =>
            processHandle != IntPtr.Zero && !IsProcessExited();

        // ── Init ──────────────────────────────────────────────────────────────

        internal GameMemorySH3CScanner()
        {
            gameMemoryValues = new GameMemorySH3C();
        }

        internal void Initialize(Process process)
        {
            if (process == null) return;
            ReleaseHandle();

            processId     = (uint)process.Id;
            processHandle = OpenProcess(
                PROCESS_VM_READ | PROCESS_QUERY_INFORMATION,
                bInheritHandle: false,
                processId);

            processFound = processHandle != IntPtr.Zero;
            if (!processFound) return;

            try
            {
                baseAddress = (ulong)(long)process.MainModule.BaseAddress;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                baseAddress = 0x400000; // Standard load address fallback.
            }
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        internal IGameMemorySH3C Refresh()
        {
            if (baseAddress != 0)
            {
                // ── Player HP ─────────────────────────────────────────────
                float hp = ReadFloat(baseAddress + OFFSET_PLAYER_HP);
                gameMemoryValues._player = new GamePlayer(hp);

                // ── In-Game Time ──────────────────────────────────────────
                gameMemoryValues._igt = ReadFloat(baseAddress + OFFSET_IGT);

                // ── Frame Rate ────────────────────────────────────────────
                gameMemoryValues._fps = ReadInt16(baseAddress + OFFSET_FPS);

                // ── Weapons ───────────────────────────────────────────────
                gameMemoryValues._handgunCount    = ReadInt16(baseAddress + OFFSET_HANDGUN);
                gameMemoryValues._shotgunCount    = ReadInt16(baseAddress + OFFSET_SHOTGUN);
                gameMemoryValues._machineGunCount = ReadInt16(baseAddress + OFFSET_MACHINEGUN);

                // ── Run Stats ─────────────────────────────────────────────
                gameMemoryValues._saveCount     = ReadByte(baseAddress + OFFSET_SAVES);
                gameMemoryValues._itemCount     = ReadInt16(baseAddress + OFFSET_ITEMS);
                gameMemoryValues._shootingKills = ReadInt16(baseAddress + OFFSET_SHOOTING);
                gameMemoryValues._meleeKills    = ReadInt16(baseAddress + OFFSET_MELEE);
                gameMemoryValues._damageTaken   = ReadFloat(baseAddress + OFFSET_DAMAGE_TAKEN);

                // ── Difficulty ────────────────────────────────────────────
                gameMemoryValues._actionDifficulty = ReadByte(baseAddress + OFFSET_ACTION_DIFF);
                gameMemoryValues._riddleDifficulty = ReadByte(baseAddress + OFFSET_RIDDLE_DIFF);

                // ── Boss Fight Times ──────────────────────────────────────
                gameMemoryValues._wormTime       = ReadFloat(baseAddress + OFFSET_WORM_TIME);
                gameMemoryValues._missionaryTime = ReadFloat(baseAddress + OFFSET_MISSIONARY_TIME);
                gameMemoryValues._leonardTime    = ReadFloat(baseAddress + OFFSET_LEONARD_TIME);
                gameMemoryValues._alessaTime     = ReadFloat(baseAddress + OFFSET_ALESSA_TIME);
                gameMemoryValues._godTime        = ReadFloat(baseAddress + OFFSET_GOD_TIME);
            }

            HasScanned = true;
            return gameMemoryValues;
        }

        // ── Low-level reads ───────────────────────────────────────────────────

        private byte ReadByte(ulong address)
        {
            var buf = new byte[1];
            ReadProcessMemory(processHandle, address, buf, 1, out int read);
            return read == 1 ? buf[0] : (byte)0;
        }

        private short ReadInt16(ulong address)
        {
            var buf = new byte[2];
            ReadProcessMemory(processHandle, address, buf, 2, out int read);
            return read == 2 ? BitConverter.ToInt16(buf, 0) : (short)0;
        }

        private float ReadFloat(ulong address)
        {
            var buf = new byte[4];
            ReadProcessMemory(processHandle, address, buf, 4, out int read);
            return read == 4 ? BitConverter.ToSingle(buf, 0) : 0f;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool IsProcessExited()
        {
            try   { return Process.GetProcessById((int)processId).HasExited; }
            catch { return true; }
        }

        private void ReleaseHandle()
        {
            if (processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
            }
            baseAddress  = 0;
            processFound = false;
            HasScanned   = false;
        }

        // ── IDisposable ───────────────────────────────────────────────────────
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) { ReleaseHandle(); disposedValue = true; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~GameMemorySH3CScanner() => Dispose(false);
    }
}
