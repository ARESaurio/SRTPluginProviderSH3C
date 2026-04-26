using SRTPluginBase;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace SRTPluginProviderSH3C
{
    public class SRTPluginProviderSH3C : IPluginProvider
    {
        private Process process;
        private GameMemorySH3CScanner gameMemoryScanner;
        private IPluginHostDelegates hostDelegates;

        public IPluginInfo Info => new PluginInfo();

        public bool GameRunning => true;

        public int Startup(IPluginHostDelegates hostDelegates)
        {
            this.hostDelegates = hostDelegates;
            gameMemoryScanner = new GameMemorySH3CScanner();
            process = GetProcess();
            if (process != null)
                gameMemoryScanner.Initialize(process);
            return 0;
        }

        public int Shutdown()
        {
            gameMemoryScanner?.Dispose();
            gameMemoryScanner = null;
            process = null;
            return 0;
        }

        public object PullData()
        {
            try
            {
                // Re-attach whenever the game process is lost.
                if (!gameMemoryScanner.ProcessRunning)
                {
                    process = GetProcess();
                    if (process != null)
                        gameMemoryScanner.Initialize(process);
                }

                if (!gameMemoryScanner.ProcessRunning)
                    return null;

                return gameMemoryScanner.Refresh();
            }
            catch (Win32Exception ex)
            {
                // ERROR_PARTIAL_COPY (299) is expected when the game exits mid-read.
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

        private Process GetProcess() => Process.GetProcesses()
            .Where(a => a.ProcessName.StartsWith("sh3", StringComparison.InvariantCultureIgnoreCase))
            .FirstOrDefault();
    }
}
