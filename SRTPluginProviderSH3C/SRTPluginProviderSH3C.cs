using SRTPluginBase;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace SRTPluginProviderSH3C
{
    public class SRTPluginProviderSH3C : IPluginProvider
    {
        // Active scanner — either PC or PS2, depending on which process is found.
        private IScanner             activeScanner;
        private Process              process;
        private IPluginHostDelegates hostDelegates;

        public IPluginInfo Info      => new PluginInfo();
        public bool        GameRunning => true;

        public int Startup(IPluginHostDelegates hostDelegates)
        {
            this.hostDelegates = hostDelegates;
            TryAttach();
            return 0;
        }

        public int Shutdown()
        {
            activeScanner?.Dispose();
            activeScanner = null;
            process       = null;
            return 0;
        }

        public object PullData()
        {
            try
            {
                if (activeScanner == null || !activeScanner.ProcessRunning)
                    TryAttach();

                if (activeScanner == null || !activeScanner.ProcessRunning)
                    return null;

                return activeScanner.Refresh();
            }
            catch (Win32Exception ex)
            {
                // ERROR_PARTIAL_COPY (299) is expected when the process exits mid-read.
                if (ex.NativeErrorCode != 299)
                    hostDelegates.ExceptionMessage.Invoke(ex);
                return null;
            }
            catch (Exception ex)
            {
                hostDelegates.ExceptionMessage.Invoke(ex);
                return null;
            }
        }

        // ── Process Detection ─────────────────────────────────────────────────

        private void TryAttach()
        {
            activeScanner?.Dispose();
            activeScanner = null;

            // PC version takes priority.
            process = FindProcess("sh3");
            if (process != null)
            {
                var scanner = new GameMemorySH3CScanner();
                scanner.Initialize(process);
                activeScanner = new PCScannerWrapper(scanner);
                return;
            }

            // PS2 version via PCSX2-Qt (also tries legacy "pcsx2" process name).
            process = FindProcess("pcsx2-qt") ?? FindProcess("pcsx2");
            if (process != null)
            {
                var scanner = new GameMemorySH3PS2Scanner();
                scanner.Initialize(process);
                activeScanner = new PS2ScannerWrapper(scanner);
            }
        }

        private static Process FindProcess(string name) =>
            Process.GetProcesses()
                   .Where(p => p.ProcessName.StartsWith(
                       name, StringComparison.InvariantCultureIgnoreCase))
                   .FirstOrDefault();

        // ── Scanner Adapters ──────────────────────────────────────────────────
        // Thin wrappers so the provider can hold either scanner behind one interface.

        private interface IScanner : IDisposable
        {
            bool   ProcessRunning { get; }
            object Refresh();
        }

        private sealed class PCScannerWrapper : IScanner
        {
            private readonly GameMemorySH3CScanner inner;
            public PCScannerWrapper(GameMemorySH3CScanner s) => inner = s;
            public bool   ProcessRunning => inner.ProcessRunning;
            public object Refresh()      => inner.Refresh();
            public void   Dispose()      => inner.Dispose();
        }

        private sealed class PS2ScannerWrapper : IScanner
        {
            private readonly GameMemorySH3PS2Scanner inner;
            public PS2ScannerWrapper(GameMemorySH3PS2Scanner s) => inner = s;
            public bool   ProcessRunning => inner.ProcessRunning;
            public object Refresh()      => inner.Refresh();
            public void   Dispose()      => inner.Dispose();
        }
    }
}
