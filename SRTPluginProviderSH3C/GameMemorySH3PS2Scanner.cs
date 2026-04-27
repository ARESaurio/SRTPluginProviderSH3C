using SRTPluginProviderSH3C.Structs.GameStructs;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SRTPluginProviderSH3C
{
    /// <summary>
    /// Reads Silent Hill 3 (PS2) memory from the PCSX2 emulator process.
    /// Supports PCSX2-Qt 2.x (tested on 2.6.3).
    ///
    /// Strategy: scan PCSX2's virtual address space for the PS2 EE RAM block
    /// (a 32 MB committed region), then verify it belongs to SH3 by checking
    /// that Heather's HP float is within the valid range [0.0, 100.0].
    ///
    /// Source: RetroAchievements Code Notes — Silent Hill 3 (PS2).
    /// </summary>
    public class GameMemorySH3PS2Scanner : IDisposable
    {
        // ── Win32 P/Invoke ────────────────────────────────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, ulong lpBaseAddress,
            byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern nint VirtualQueryEx(IntPtr hProcess, ulong lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public ulong BaseAddress;
            public ulong AllocationBase;
            public uint  AllocationProtect;
            public uint  __alignment1;
            public ulong RegionSize;
            public uint  State;
            public uint  Protect;
            public uint  Type;
            public uint  __alignment2;
        }

        private const uint PROCESS_VM_READ           = 0x0010;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint MEM_COMMIT                = 0x1000;
        private const uint PAGE_READWRITE            = 0x04;

        // ── PS2 EE RAM ────────────────────────────────────────────────────────
        private const ulong EE_RAM_SIZE = 0x2000000UL; // 32 MB

        // ── PS2 Memory Offsets ────────────────────────────────────────────────
        // Source: RetroAchievements Code Notes for Silent Hill 3 (PS2).
        // All offsets are relative to the start of the EE RAM block.

        // Player (entity buffer)
        private const ulong OFFSET_HP         = 0x3D4AEC; // float, max 100.0

        // In-Game Time
        private const ulong OFFSET_IGT        = 0x1D87FEC; // 32-bit float (seconds)

        // Weapon clip ammo (currently loaded magazine)
        private const ulong OFFSET_PISTOL_CLIP  = 0x1D31C12;
        private const ulong OFFSET_SHOTGUN_CLIP  = 0x1D31C14;
        private const ulong OFFSET_SMG_CLIP      = 0x1D31C16;

        // Weapon reserve ammo
        private const ulong OFFSET_PISTOL_RESERVE  = 0x1D31C1C;
        private const ulong OFFSET_SHOTGUN_RESERVE  = 0x1D31C1E;
        private const ulong OFFSET_SMG_RESERVE      = 0x1D31C20;

        // Items
        private const ulong OFFSET_BEEF_JERKY = 0x1D31C28;

        // Run stats
        private const ulong OFFSET_SAVES      = 0x1D87FDC;
        private const ulong OFFSET_ITEMS      = 0x1D87FE0;
        private const ulong OFFSET_SHOOTING   = 0x1D87FE2;
        private const ulong OFFSET_MELEE      = 0x1D87FE4;
        private const ulong OFFSET_DAMAGE     = 0x1D87FF8;

        // Difficulty
        private const ulong OFFSET_ACTION_DIFF = 0x1D87FD6;
        private const ulong OFFSET_RIDDLE_DIFF = 0x1D87FD7;

        // Game area
        private const ulong OFFSET_GAME_AREA  = 0x1D87FD1;

        // Boss kill times (32-bit float, seconds)
        private const ulong OFFSET_WORM_TIME       = 0x1D87FFC;
        private const ulong OFFSET_MISSIONARY_TIME = 0x1D88000;
        private const ulong OFFSET_LEONARD_TIME    = 0x1D88004;
        private const ulong OFFSET_ALESSA_TIME     = 0x1D88008;
        private const ulong OFFSET_GOD_TIME        = 0x1D8800C;

        // ── State ─────────────────────────────────────────────────────────────
        private IntPtr            processHandle   = IntPtr.Zero;
        private uint              processId;
        private ulong             eeRamBase       = 0;
        private bool              processFound    = false;
        private GameMemorySH3PS2  gameMemoryValues;

        public bool HasScanned    { get; private set; }
        public bool ProcessRunning =>
            processHandle != IntPtr.Zero && !IsProcessExited();

        // ── Init ──────────────────────────────────────────────────────────────

        internal GameMemorySH3PS2Scanner()
        {
            gameMemoryValues = new GameMemorySH3PS2();
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

            eeRamBase = FindEERAMBase();
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        internal IGameMemorySH3PS2 Refresh()
        {
            // Re-scan for EE RAM if we haven't found it yet (game may not have
            // loaded when we first attached).
            if (eeRamBase == 0 && processHandle != IntPtr.Zero)
                eeRamBase = FindEERAMBase();

            if (eeRamBase != 0)
            {
                // ── Player HP ────────────────────────────────────────────
                float hp = ReadFloat(eeRamBase + OFFSET_HP);
                gameMemoryValues._player = new GamePlayer(hp);

                // ── In-Game Time ─────────────────────────────────────────
                gameMemoryValues._igt = ReadFloat(eeRamBase + OFFSET_IGT);

                // ── Weapons: clip ─────────────────────────────────────────
                gameMemoryValues._pistolClip    = ReadInt16(eeRamBase + OFFSET_PISTOL_CLIP);
                gameMemoryValues._shotgunClip   = ReadInt16(eeRamBase + OFFSET_SHOTGUN_CLIP);
                gameMemoryValues._smgClip       = ReadInt16(eeRamBase + OFFSET_SMG_CLIP);

                // ── Weapons: reserve ─────────────────────────────────────
                gameMemoryValues._pistolReserve  = ReadInt16(eeRamBase + OFFSET_PISTOL_RESERVE);
                gameMemoryValues._shotgunReserve = ReadInt16(eeRamBase + OFFSET_SHOTGUN_RESERVE);
                gameMemoryValues._smgReserve     = ReadInt16(eeRamBase + OFFSET_SMG_RESERVE);

                // ── Items ─────────────────────────────────────────────────
                gameMemoryValues._beefJerky = ReadInt16(eeRamBase + OFFSET_BEEF_JERKY);

                // ── Run Stats ─────────────────────────────────────────────
                gameMemoryValues._saveCount     = ReadByte(eeRamBase + OFFSET_SAVES);
                gameMemoryValues._itemCount     = ReadInt16(eeRamBase + OFFSET_ITEMS);
                gameMemoryValues._shootingKills = ReadInt16(eeRamBase + OFFSET_SHOOTING);
                gameMemoryValues._meleeKills    = ReadInt16(eeRamBase + OFFSET_MELEE);
                gameMemoryValues._damageTaken   = ReadInt16(eeRamBase + OFFSET_DAMAGE);

                // ── Difficulty ────────────────────────────────────────────
                gameMemoryValues._actionDifficulty = ReadByte(eeRamBase + OFFSET_ACTION_DIFF);
                gameMemoryValues._riddleDifficulty = ReadByte(eeRamBase + OFFSET_RIDDLE_DIFF);

                // ── Game Area ─────────────────────────────────────────────
                gameMemoryValues._gameArea = ReadByte(eeRamBase + OFFSET_GAME_AREA);

                // ── Boss Fight Times ──────────────────────────────────────
                gameMemoryValues._wormTime       = ReadFloat(eeRamBase + OFFSET_WORM_TIME);
                gameMemoryValues._missionaryTime = ReadFloat(eeRamBase + OFFSET_MISSIONARY_TIME);
                gameMemoryValues._leonardTime    = ReadFloat(eeRamBase + OFFSET_LEONARD_TIME);
                gameMemoryValues._alessaTime     = ReadFloat(eeRamBase + OFFSET_ALESSA_TIME);
                gameMemoryValues._godTime        = ReadFloat(eeRamBase + OFFSET_GOD_TIME);
            }

            HasScanned = true;
            return gameMemoryValues;
        }

        // ── EE RAM Detection ──────────────────────────────────────────────────

        /// <summary>
        /// Scans PCSX2's virtual address space for the 32 MB PS2 EE RAM block.
        /// Verifies the block belongs to SH3 by confirming Heather's HP is
        /// within the valid range [0.0, 100.0].
        /// </summary>
        private ulong FindEERAMBase()
        {
            ulong address = 0;
            const ulong MAX_ADDR = 0x00007FFFFFFFFFFFUL;
            int mbiSize = Marshal.SizeOf<MEMORY_BASIC_INFORMATION>();

            while (address < MAX_ADDR)
            {
                if (VirtualQueryEx(processHandle, address, out var mbi, (uint)mbiSize) == 0)
                    break;

                if (mbi.RegionSize == EE_RAM_SIZE && mbi.State == MEM_COMMIT)
                {
                    float hp = ReadFloat(mbi.BaseAddress + OFFSET_HP);
                    if (hp >= 0.0f && hp <= 100.001f && !float.IsNaN(hp))
                        return mbi.BaseAddress;
                }

                ulong next = mbi.BaseAddress + Math.Max(mbi.RegionSize, 1UL);
                if (next <= address) break; // overflow guard
                address = next;
            }

            return 0;
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
            eeRamBase    = 0;
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

        ~GameMemorySH3PS2Scanner() => Dispose(false);
    }
}
