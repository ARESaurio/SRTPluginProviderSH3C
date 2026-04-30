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
    /// (committed region >= 32 MB), then verify it belongs to SH3 by checking
    /// that difficulty bytes, HP, and IGT are all within valid ranges.
    /// This avoids false-positives on zeroed or unrelated memory blocks.
    ///
    /// Source: RetroAchievements Code Notes â€” Silent Hill 3 (PS2).
    /// </summary>
    public class GameMemorySH3PS2Scanner : IDisposable
    {
        // â”€â”€ Win32 P/Invoke â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ PS2 EE RAM â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private const ulong EE_RAM_SIZE = 0x2000000UL; // 32 MB

        // â”€â”€ NTSC-U (USA) Memory Offsets â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Source: RetroAchievements Code Notes â€” Silent Hill 3 (USA) SLUS-20707.

        private const ulong USA_HP            = 0x3D4AEC;
        private const ulong USA_IGT           = 0x1D87FEC;
        private const ulong USA_PISTOL_CLIP   = 0x1D31C12;
        private const ulong USA_SHOTGUN_CLIP  = 0x1D31C14;
        private const ulong USA_SMG_CLIP      = 0x1D31C16;
        private const ulong USA_PISTOL_RES    = 0x1D31C1C;
        private const ulong USA_SHOTGUN_RES   = 0x1D31C1E;
        private const ulong USA_SMG_RES       = 0x1D31C20;
        private const ulong USA_BEEF_JERKY    = 0x1D31C28;
        private const ulong USA_SAVES         = 0x1D87FDC;
        private const ulong USA_ITEMS         = 0x1D87FE0;
        private const ulong USA_SHOOTING      = 0x1D87FE2;
        private const ulong USA_MELEE         = 0x1D87FE4;
        private const ulong USA_DAMAGE        = 0x1D87FF8;
        private const ulong USA_ACTION_DIFF   = 0x1D87FD6;
        private const ulong USA_RIDDLE_DIFF   = 0x1D87FD7;
        private const ulong USA_GAME_AREA     = 0x1D87FD1;
        private const ulong USA_WORM_TIME     = 0x1D87FFC;
        private const ulong USA_MISS_TIME     = 0x1D88000;
        private const ulong USA_LEON_TIME     = 0x1D88004;
        private const ulong USA_ALESS_TIME    = 0x1D88008;
        private const ulong USA_GOD_TIME      = 0x1D8800C;

        // â”€â”€ PAL (EU) Memory Offsets â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Confirmed by live diagnostic on SLES-51434 with PCSX2 2.6.3.
        // HP and IGT verified; ammo/stats/area offsets pending confirmation.
        // PAL entity buffer is at a different PS2 physical address than USA.

        private const ulong PAL_HP            = 0x45A8F0; // confirmed
        private const ulong PAL_IGT           = 0x1D80EA0; // confirmed
        // TODO: remaining PAL offsets â€” pending in-game verification.
        // Using 0 as sentinel so Refresh() skips them until confirmed.
        private const ulong PAL_PISTOL_CLIP   = 0;
        private const ulong PAL_SHOTGUN_CLIP  = 0;
        private const ulong PAL_SMG_CLIP      = 0;
        private const ulong PAL_PISTOL_RES    = 0;
        private const ulong PAL_SHOTGUN_RES   = 0;
        private const ulong PAL_SMG_RES       = 0;
        private const ulong PAL_BEEF_JERKY    = 0;
        private const ulong PAL_SAVES         = 0;
        private const ulong PAL_ITEMS         = 0;
        private const ulong PAL_SHOOTING      = 0;
        private const ulong PAL_MELEE         = 0;
        private const ulong PAL_DAMAGE        = 0;
        private const ulong PAL_ACTION_DIFF   = 0;
        private const ulong PAL_RIDDLE_DIFF   = 0;
        private const ulong PAL_GAME_AREA     = 0;
        private const ulong PAL_WORM_TIME     = 0;
        private const ulong PAL_MISS_TIME     = 0;
        private const ulong PAL_LEON_TIME     = 0;
        private const ulong PAL_ALESS_TIME    = 0;
        private const ulong PAL_GOD_TIME      = 0;

        // â”€â”€ Active offset aliases (set after region detection) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private ulong OFFSET_HP;          private ulong OFFSET_IGT;
        private ulong OFFSET_PISTOL_CLIP; private ulong OFFSET_SHOTGUN_CLIP;
        private ulong OFFSET_SMG_CLIP;    private ulong OFFSET_PISTOL_RESERVE;
        private ulong OFFSET_SHOTGUN_RESERVE; private ulong OFFSET_SMG_RESERVE;
        private ulong OFFSET_BEEF_JERKY;  private ulong OFFSET_SAVES;
        private ulong OFFSET_ITEMS;       private ulong OFFSET_SHOOTING;
        private ulong OFFSET_MELEE;       private ulong OFFSET_DAMAGE;
        private ulong OFFSET_ACTION_DIFF; private ulong OFFSET_RIDDLE_DIFF;
        private ulong OFFSET_GAME_AREA;
        private ulong OFFSET_WORM_TIME;   private ulong OFFSET_MISSIONARY_TIME;
        private ulong OFFSET_LEONARD_TIME; private ulong OFFSET_ALESSA_TIME;
        private ulong OFFSET_GOD_TIME;

        // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private IntPtr            processHandle   = IntPtr.Zero;
        private uint              processId;
        private long              _eeRamBase      = 0;  // backing; use Interlocked
        private ulong             eeRamBase
        {
            get => (ulong)System.Threading.Interlocked.Read(ref _eeRamBase);
            set => System.Threading.Interlocked.Exchange(ref _eeRamBase, (long)value);
        }
        private bool              processFound    = false;
        private bool              isPalPS2        = false;
        private bool              palScanRunning  = false;
        private GameMemorySH3PS2  gameMemoryValues;

        public bool HasScanned    { get; private set; }
        public bool ProcessRunning =>
            processHandle != IntPtr.Zero && !IsProcessExited();

        // â”€â”€ Init â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            // Pass 1: USA â€” fast synchronous scan (AoB, no sleep).
            ApplyOffsets(pal: false);
            ulong usa = ScanForHP(
                0x00007FFFFFFFFFFFUL, 0x100000UL,
                Marshal.SizeOf<MEMORY_BASIC_INFORMATION>(),
                USA_HP, c => VerifyUSABlock(c));

            if (usa != 0)
            {
                isPalPS2  = false;
                eeRamBase = usa;
                return;
            }

            // Pass 2: PAL â€” slow IGT-delta scan, run on background thread
            // so SRTHost is not blocked during startup.
            ApplyOffsets(pal: true);
            isPalPS2      = true;
            palScanRunning = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                // Retry loop: scan every ~3s until EE RAM found or process gone.
                while (processHandle != IntPtr.Zero)
                {
                    ulong pal = ScanForPALViaIGT(
                        0x00007FFFFFFFFFFFUL, 0x100000UL,
                        Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
                    if (pal != 0)
                    {
                        eeRamBase = pal;
                        if (_palIgtOffset != 0) OFFSET_IGT = _palIgtOffset;
                        break;
                    }
                    System.Threading.Thread.Sleep(3000); // retry if game not running yet
                }
                palScanRunning = false;
            });
        }

        // â”€â”€ Refresh â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        internal IGameMemorySH3PS2 Refresh()
        {
            // If USA scan found nothing and PAL background scan is complete,
            // the eeRamBase will have been set by the background Task.
            // Do NOT call FindEERAMBase() synchronously here â€” it would block.

            if (eeRamBase != 0)
            {
                // â”€â”€ Player HP â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                float hp = ReadFloat(eeRamBase + OFFSET_HP);
                gameMemoryValues._player = new GamePlayer(hp);

                // â”€â”€ In-Game Time â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                gameMemoryValues._igt = ReadFloat(eeRamBase + OFFSET_IGT);

                // â”€â”€ Weapons: clip (skipped when offset not confirmed for region) â”€â”€
                if (OFFSET_PISTOL_CLIP  != 0) gameMemoryValues._pistolClip    = ReadInt16(eeRamBase + OFFSET_PISTOL_CLIP);
                if (OFFSET_SHOTGUN_CLIP != 0) gameMemoryValues._shotgunClip   = ReadInt16(eeRamBase + OFFSET_SHOTGUN_CLIP);
                if (OFFSET_SMG_CLIP     != 0) gameMemoryValues._smgClip       = ReadInt16(eeRamBase + OFFSET_SMG_CLIP);

                // â”€â”€ Weapons: reserve â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                if (OFFSET_PISTOL_RESERVE  != 0) gameMemoryValues._pistolReserve  = ReadInt16(eeRamBase + OFFSET_PISTOL_RESERVE);
                if (OFFSET_SHOTGUN_RESERVE != 0) gameMemoryValues._shotgunReserve = ReadInt16(eeRamBase + OFFSET_SHOTGUN_RESERVE);
                if (OFFSET_SMG_RESERVE     != 0) gameMemoryValues._smgReserve     = ReadInt16(eeRamBase + OFFSET_SMG_RESERVE);

                // â”€â”€ Items â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                if (OFFSET_BEEF_JERKY != 0) gameMemoryValues._beefJerky = ReadInt16(eeRamBase + OFFSET_BEEF_JERKY);

                // â”€â”€ Run Stats â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                if (OFFSET_SAVES    != 0) gameMemoryValues._saveCount     = ReadByte(eeRamBase + OFFSET_SAVES);
                if (OFFSET_ITEMS    != 0) gameMemoryValues._itemCount     = ReadInt16(eeRamBase + OFFSET_ITEMS);
                if (OFFSET_SHOOTING != 0) gameMemoryValues._shootingKills = ReadInt16(eeRamBase + OFFSET_SHOOTING);
                if (OFFSET_MELEE    != 0) gameMemoryValues._meleeKills    = ReadInt16(eeRamBase + OFFSET_MELEE);
                if (OFFSET_DAMAGE   != 0) gameMemoryValues._damageTaken   = ReadInt16(eeRamBase + OFFSET_DAMAGE);

                // â”€â”€ Difficulty â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                if (OFFSET_ACTION_DIFF != 0) gameMemoryValues._actionDifficulty = ReadByte(eeRamBase + OFFSET_ACTION_DIFF);
                if (OFFSET_RIDDLE_DIFF != 0) gameMemoryValues._riddleDifficulty = ReadByte(eeRamBase + OFFSET_RIDDLE_DIFF);

                // â”€â”€ Game Area â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                if (OFFSET_GAME_AREA != 0) gameMemoryValues._gameArea = ReadByte(eeRamBase + OFFSET_GAME_AREA);

                // â”€â”€ Boss Fight Times â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                if (OFFSET_WORM_TIME       != 0) gameMemoryValues._wormTime       = ReadFloat(eeRamBase + OFFSET_WORM_TIME);
                if (OFFSET_MISSIONARY_TIME != 0) gameMemoryValues._missionaryTime = ReadFloat(eeRamBase + OFFSET_MISSIONARY_TIME);
                if (OFFSET_LEONARD_TIME    != 0) gameMemoryValues._leonardTime    = ReadFloat(eeRamBase + OFFSET_LEONARD_TIME);
                if (OFFSET_ALESSA_TIME     != 0) gameMemoryValues._alessaTime     = ReadFloat(eeRamBase + OFFSET_ALESSA_TIME);
                if (OFFSET_GOD_TIME        != 0) gameMemoryValues._godTime        = ReadFloat(eeRamBase + OFFSET_GOD_TIME);
            }

            HasScanned = true;
            return gameMemoryValues;
        }

        // â”€â”€ EE RAM Detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // HP=100.0f in little-endian IEEE-754: bytes [0x00, 0x00, 0xC8, 0x42].
        // This is Heather's full health value, present at game start and on resets.
        private static readonly byte[] HP_FULL_PATTERN = { 0x00, 0x00, 0xC8, 0x42 };

        /// <summary>
        /// Finds the PS2 EE RAM base by scanning PCSX2's committed memory for
        /// Heather's full HP value (100.0f = [00 00 C8 42]) at 4-byte aligned
        /// positions, then computing candidate = hit_address âˆ’ HP_OFFSET.
        ///
        /// This approach works because:
        ///   - The EE RAM may not start at the beginning of any VirtualQueryEx
        ///     region; it can be in the middle of a larger virtual reservation.
        ///   - Once the EE base is found it is cached for the session, so
        ///     subsequent reads work even when HP drops to 0.
        ///
        /// Requires HP = 100.0f (full health). For speedrunners this is always
        /// true on game start / reset. Re-scans on each Refresh() until found.
        /// </summary>
        private ulong FindEERAMBase()
        {
            const ulong MAX_ADDR   = 0x00007FFFFFFFFFFFUL;
            const ulong MIN_REGION = 0x100000UL;
            int mbiSize = Marshal.SizeOf<MEMORY_BASIC_INFORMATION>();

            // -- Pass 1: USA (NTSC-U, SLUS-20707) --------------------------------
            // HP=100.0f at PS2 offset 0x3D4AEC. Validate with USA Area/Difficulty.
            ApplyOffsets(pal: false);
            ulong result = ScanForHP(MAX_ADDR, MIN_REGION, mbiSize, USA_HP,
                                     candidate => VerifyUSABlock(candidate));
            if (result != 0) { isPalPS2 = false; return result; }

            // -- Pass 2: PAL (EU, SLES-51434) -------------------------------------
            // Use IGT offset 0x1D80EA0 (confirmed) as the anchor instead of HP=100.
            // This works regardless of Heather's current HP.
            ApplyOffsets(pal: true);
            result = ScanForPALViaIGT(MAX_ADDR, MIN_REGION, mbiSize);
            if (result != 0) { isPalPS2 = true; return result; }

            // No match found this cycle. Keep offsets at USA defaults.
            ApplyOffsets(pal: false);
            return 0;
        }

        private ulong ScanForHP(ulong maxAddr, ulong minRegion, int mbiSize,
                                 ulong hpOffset, Func<ulong, bool> verify)
        {
            ulong address = 0;
            while (address < maxAddr)
            {
                if (VirtualQueryEx(processHandle, address, out var mbi, (uint)mbiSize) == 0)
                    break;

                if (mbi.State == MEM_COMMIT && mbi.RegionSize >= minRegion &&
                    (mbi.Protect == PAGE_READWRITE || (mbi.Protect & PAGE_READWRITE) != 0))
                {
                    int readLen = (int)Math.Min(mbi.RegionSize, EE_RAM_SIZE + 0x100000UL);
                    var buf = new byte[readLen];
                    ReadProcessMemory(processHandle, mbi.BaseAddress, buf, readLen, out int bytesRead);

                    for (int i = 0; i <= bytesRead - 4; i += 4)
                    {
                        if (buf[i]     != HP_FULL_PATTERN[0]) continue;
                        if (buf[i + 1] != HP_FULL_PATTERN[1]) continue;
                        if (buf[i + 2] != HP_FULL_PATTERN[2]) continue;
                        if (buf[i + 3] != HP_FULL_PATTERN[3]) continue;

                        ulong hitAddr = mbi.BaseAddress + (ulong)i;
                        if (hitAddr < hpOffset) continue;

                        ulong candidate = hitAddr - hpOffset;
                        if ((candidate & 0xFFFUL) != 0) continue;

                        if (verify(candidate)) return candidate;
                    }
                }

                ulong next = mbi.BaseAddress + Math.Max(mbi.RegionSize, 1UL);
                if (next <= address) break;
                address = next;
            }
            return 0;
        }

        /// <summary>
        /// PAL-specific scan. PCSX2 2.x commits EE RAM as many tiny VirtualQueryEx
        /// segments within a single large virtual reservation â€” RegionSize cannot be
        /// used to detect the full 32 MB EE RAM. Instead, collect unique AllocBases
        /// from all committed readable segments, then for each allocation probe
        /// page-aligned EE base candidates using ReadProcessMemory (which spans
        /// committed pages within one allocation). If PAL_IGT reads a valid, ticking
        /// float, we found the PAL EE RAM. Does NOT require HP=100.
        /// </summary>
        // Discovered PAL IGT offset (dynamic per session, found at scan time).
        private ulong _palIgtOffset = 0;

        private ulong ScanForPALViaIGT(ulong maxAddr, ulong minRegion, int mbiSize)
        {
            // The PAL IGT PS2 physical offset is NOT constant between PCSX2 sessions.
            // We find it dynamically: snapshot all committed RW floats, sleep 400ms,
            // re-read, and look for a float that increased by ~0.08-0.8s (timer tick).
            // That float's host address = EE_base + IGT_PS2_offset.
            // We then back-compute EE_base by aligning to 4 KB (page boundary).

            // -- Pass 1: read qualifying blocks (dual-buffer approach) ─────────
            var blocks = new System.Collections.Generic.List<System.Tuple<ulong, byte[]>>();
            ulong addr = 0x00007F0000000000UL;
            while (addr < maxAddr) {
                if (VirtualQueryEx(processHandle, addr, out var mbi, (uint)mbiSize) == 0) break;
                if (mbi.State == MEM_COMMIT && mbi.RegionSize >= 0x100000UL && mbi.RegionSize <= 0x6000000UL &&
                    (mbi.Protect == PAGE_READWRITE || (mbi.Protect & PAGE_READWRITE) != 0)) {
                    int rLen = (int)Math.Min(mbi.RegionSize, 0x2000000UL);
                    var buf1 = new byte[rLen];
                    ReadProcessMemory(processHandle, mbi.BaseAddress, buf1, rLen, out int _);
                    blocks.Add(System.Tuple.Create(mbi.BaseAddress, buf1));
                }
                ulong nx = mbi.BaseAddress + Math.Max(mbi.RegionSize, 1UL);
                if (nx <= addr) break; addr = nx;
            }
            if (blocks.Count == 0) return 0;

            // -- Sleep exactly 2 seconds ──
            System.Threading.Thread.Sleep(2000);

            // -- Pass 2: re-read same blocks, compare buffer-to-buffer (exact 2s delta) ─────
            // Try every ticking float in every block until HP validates the EE base.
            const ulong IGT_MIN = 0x1D70000UL;
            const ulong IGT_MAX = 0x1DA0000UL;
            foreach (var blk in blocks) {
                int rLen2 = blk.Item2.Length;
                var buf2 = new byte[rLen2];
                ReadProcessMemory(processHandle, blk.Item1, buf2, rLen2, out int br2);
                for (int i = 0; i <= br2 - 4; i += 4) {
                    float v1 = BitConverter.ToSingle(blk.Item2, i);
                    float v2b = BitConverter.ToSingle(buf2, i);
                    if (float.IsNaN(v1)||float.IsNaN(v2b)||float.IsInfinity(v1)||float.IsInfinity(v2b)) continue;
                    if (v1 < 1.0f || v1 >= 36000f) continue;
                    float delta = v2b - v1;
                    if (delta <= 1.5f || delta >= 2.5f) continue;
                    // This float ticks at ~1s/s. Try it as IGT candidate.
                    ulong igtHostAddr = blk.Item1 + (ulong)i;
                    ulong lower   = igtHostAddr & 0xFFFUL;
                    ulong igtOff0 = (IGT_MIN & ~0xFFFUL) | lower;
                    if (igtOff0 < IGT_MIN) igtOff0 += 0x1000UL;
                    for (ulong igtOff = igtOff0; igtOff <= IGT_MAX; igtOff += 0x1000UL) {
                        if (igtHostAddr < igtOff) break;
                        ulong candidate = igtHostAddr - igtOff;
                        // Non-circular: validate HP at known PAL HP offset (confirmed by PALFull).
                        float hp = ReadFloat(candidate + 0x47DA10UL);
                        if (hp < 0.1f || hp > 100f || float.IsNaN(hp)) continue;
                        // EE base confirmed. Now find real IGT by scanning buf1 vs buf2
                        // directly in EE offset range [0x1D70000, 0x1DA0000].
                        // This avoids accepting a wrong oscillating timer as IGT.
                        ulong blkEeOff = blk.Item1 - candidate; // block start in PS2 space
                        if (blkEeOff > 0x1DA0000UL) continue;   // block not in IGT range
                        long igtBufStart = (long)0x1D70000L - (long)blkEeOff;
                        long igtBufEnd   = (long)0x1DA0000L - (long)blkEeOff;
                        if (igtBufStart < 0) igtBufStart = 0;
                        if (igtBufEnd   > rLen2) igtBufEnd = rLen2;
                        ulong bestIgtOff = 0; float bestVal = 0f;
                        for (long bi = igtBufStart; bi <= igtBufEnd - 4; bi += 4) {
                            float bv1 = BitConverter.ToSingle(blk.Item2, (int)bi);
                            float bv2 = BitConverter.ToSingle(buf2,      (int)bi);
                            if (float.IsNaN(bv1)||float.IsNaN(bv2)||float.IsInfinity(bv1)||float.IsInfinity(bv2)) continue;
                            if (bv1 < 1f || bv1 >= 36000f) continue;
                            float bd = bv2 - bv1;
                            if (bd > 1.5f && bd < 2.5f && bv1 > bestVal) { bestVal = bv1; bestIgtOff = blkEeOff + (ulong)bi; }
                        }
                        if (bestIgtOff == 0) continue;
                        _palIgtOffset = bestIgtOff; return candidate;
                    }
                }
            }
            return 0;
        }

        /// <summary>Sets the active OFFSET_xxx aliases to the correct region table.</summary>
        private void ApplyOffsets(bool pal)
        {
            OFFSET_HP               = pal ? PAL_HP           : USA_HP;
            OFFSET_IGT              = pal ? PAL_IGT           : USA_IGT;
            OFFSET_PISTOL_CLIP      = pal ? PAL_PISTOL_CLIP   : USA_PISTOL_CLIP;
            OFFSET_SHOTGUN_CLIP     = pal ? PAL_SHOTGUN_CLIP  : USA_SHOTGUN_CLIP;
            OFFSET_SMG_CLIP         = pal ? PAL_SMG_CLIP      : USA_SMG_CLIP;
            OFFSET_PISTOL_RESERVE   = pal ? PAL_PISTOL_RES    : USA_PISTOL_RES;
            OFFSET_SHOTGUN_RESERVE  = pal ? PAL_SHOTGUN_RES   : USA_SHOTGUN_RES;
            OFFSET_SMG_RESERVE      = pal ? PAL_SMG_RES       : USA_SMG_RES;
            OFFSET_BEEF_JERKY       = pal ? PAL_BEEF_JERKY    : USA_BEEF_JERKY;
            OFFSET_SAVES            = pal ? PAL_SAVES         : USA_SAVES;
            OFFSET_ITEMS            = pal ? PAL_ITEMS         : USA_ITEMS;
            OFFSET_SHOOTING         = pal ? PAL_SHOOTING      : USA_SHOOTING;
            OFFSET_MELEE            = pal ? PAL_MELEE         : USA_MELEE;
            OFFSET_DAMAGE           = pal ? PAL_DAMAGE        : USA_DAMAGE;
            OFFSET_ACTION_DIFF      = pal ? PAL_ACTION_DIFF   : USA_ACTION_DIFF;
            OFFSET_RIDDLE_DIFF      = pal ? PAL_RIDDLE_DIFF   : USA_RIDDLE_DIFF;
            OFFSET_GAME_AREA        = pal ? PAL_GAME_AREA     : USA_GAME_AREA;
            OFFSET_WORM_TIME        = pal ? PAL_WORM_TIME     : USA_WORM_TIME;
            OFFSET_MISSIONARY_TIME  = pal ? PAL_MISS_TIME     : USA_MISS_TIME;
            OFFSET_LEONARD_TIME     = pal ? PAL_LEON_TIME     : USA_LEON_TIME;
            OFFSET_ALESSA_TIME      = pal ? PAL_ALESS_TIME    : USA_ALESS_TIME;
            OFFSET_GOD_TIME         = pal ? PAL_GOD_TIME      : USA_GOD_TIME;
        }

        /// <summary>
        /// Validates a candidate EE RAM base address by checking that SH3-specific
        /// fields are within their known valid ranges.
        /// HP is intentionally NOT checked here â€” the AoB scan already guarantees
        /// it equals 100.0f at the expected offset.
        /// </summary>
        private bool VerifyUSABlock(ulong baseAddr)
        {
            byte actionDiff = ReadByte(baseAddr + USA_ACTION_DIFF);
            if (actionDiff > 5) return false;
            byte riddleDiff = ReadByte(baseAddr + USA_RIDDLE_DIFF);
            if (riddleDiff > 2) return false;
            byte gameArea = ReadByte(baseAddr + USA_GAME_AREA);
            if (gameArea > 0x27) return false;
            return true;
        }

        private bool VerifyPALBlock(ulong baseAddr)
        {
            // PAL Area/Difficulty offsets not yet confirmed.
            // Use a dual IGT read (120ms apart) to confirm the timer is ticking.
            // This eliminates false positives where a random float happens to
            // be in the valid range at offset PAL_IGT.
            float igt1 = ReadFloat(baseAddr + PAL_IGT);
            if (float.IsNaN(igt1) || float.IsInfinity(igt1)) return false;
            if (igt1 <= 0f || igt1 >= 36000f) return false;

            System.Threading.Thread.Sleep(120);

            float igt2 = ReadFloat(baseAddr + PAL_IGT);
            if (float.IsNaN(igt2) || float.IsInfinity(igt2)) return false;

            // IGT must have increased (game is running) and by a plausible amount.
            float delta = igt2 - igt1;
            return delta > 0.05f && delta < 1.0f;
        }

        // â”€â”€ Low-level reads â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€ IDisposable â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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









