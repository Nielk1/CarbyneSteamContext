using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
//using System.Data.HashFunction;
using System.Globalization;
using System.IO;
using CarbyneSteamContext.Steam4NETWrapper;
//using Gameloop.Vdf;
using CarbyneSteamContext.Models.BVdf;
using CarbyneSteamContext.Models;
using System.Text.RegularExpressions;
using Steam4NET;
using System.Diagnostics;
using CarbyneSteamContext;
//using HSteamPipe = System.Int32;
//using HSteamUser = System.Int32;

namespace CarbyneSteamContextWrapper
{
    class SteamException : Exception
    {
        public SteamException(string msg)
            : base(msg)
        {
        }
    }
    public class SteamContext : IDisposable
    {
        public static bool Is32Bit()
        {
            if (IntPtr.Size == 4)
            {
                return true;
            }
            else if (IntPtr.Size == 8)
            {
                return false;
            }
            throw new InvalidOperationException("System is not 32 or 64 bit");
        }

        private CarbyneSteamContext.SteamContext WrappedContext;
        private Steam4NETProxy Steam4NETProxy;

        private static readonly object CoreInstanceMutex = new object();
        private static SteamContext CoreInstance;
        private SteamContext()
        {
            // we made the context aware it can't run in 64 bit.
            WrappedContext = CarbyneSteamContext.SteamContext.GetInstance();
            if (!Is32Bit())
            {
                Steam4NETProxy = new Steam4NETProxy(); 
            }
        }

        public static SteamContext GetInstance()
        {
            lock (CoreInstanceMutex)
            {
                if (CoreInstance == null)
                    CoreInstance = new SteamContext();
                return CoreInstance;
            }
        }

        #region Dispose
        // Flag: Has Dispose already been called?
        bool disposed = false;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                Shutdown();
            }

            // Free any unmanaged objects here.
            //
            disposed = true;
        }

        ~SteamContext()
        {
            Dispose(false);
        }
#endregion Dispose

        public bool IsInstalled(UInt64 GameID)
        {
            if(!Is32Bit())
            {
                return Steam4NETProxy.IsInstalled(GameID);
            }
            else
            {
                return WrappedContext.IsInstalled(GameID);
            }
        }

        public Process GetSteamProcess()
        {
            int pid = WrappedContext.SteamPID;
            if (pid == 0) return null;
            return Process.GetProcessById(pid);
        }

        public void InstallGame(UInt64 GameID)
        {
            CGameID gameID = new CGameID(GameID);
            switch (gameID.AppType)
            {
                // Basic Steam App
                case CGameID.EGameID.k_EGameIDTypeApp:
                    {
                        string InstallCommand = $"steam://install/{GameID}";
                        string installPath = Steamworks.GetInstallPath();
                        string steamEXE = Path.Combine(installPath, @"steam.exe");
                        Process.Start(steamEXE, InstallCommand);
                    }
                    break;
                case CGameID.EGameID.k_EGameIDTypeGameMod:
                    break;
                case CGameID.EGameID.k_EGameIDTypeShortcut:
                    break;
            }
        }

        public string[] GetGameLibraries()
        {
            try
            {
                if (!Is32Bit())
                {
                    return Steam4NETProxy.GetGameLibraries();
                }
                else
                {
                    return WrappedContext.GetGameLibraries();
                }
            }
            catch
            {
                return new string[0];
            }
        }

        public EAppUpdateError? InstallGame(UInt64 GameID, int GameLibraryIndex)
        {
            try
            {
                if (!Is32Bit())
                {
                    return Steam4NETProxy.InstallGame(GameID, GameLibraryIndex);
                }
                else
                {
                    return WrappedContext.InstallGame(GameID, GameLibraryIndex);
                }
            }
            catch
            {
                return null;
            }
        }

        public List<CarbyneSteamContext.SteamLaunchableApp> GetOwnedApps()
        {
            try
            {
                if (!Is32Bit())
                {
                    return Steam4NETProxy.GetOwnedApps();
                }
                else
                {
                    return WrappedContext.GetOwnedApps();
                }
            }
            catch
            {
                return null;
            }
        }

        public List<CarbyneSteamContext.SteamLaunchableModGoldSrc> GetGoldSrcMods()
        {
            try
            {
                if (!Is32Bit())
                {
                    return Steam4NETProxy.GetGoldSrcMods();
                }
                else
                {
                    return WrappedContext.GetGoldSrcMods();
                }
            }
            catch
            {
                return null;
            }
        }

        public List<CarbyneSteamContext.SteamLaunchableModSource> GetSourceMods()
        {
            try
            {
                if (!Is32Bit())
                {
                    return Steam4NETProxy.GetSourceMods();
                }
                else
                {
                    return WrappedContext.GetSourceMods();
                }
            }
            catch
            {
                return null;
            }
        }







        public void StartBigPicture()
        {
            string installPath = Steamworks.GetInstallPath();
            string steamEXE = Path.Combine(installPath, @"steam.exe");
            Process.Start(steamEXE, "steam://open/bigpicture");
        }

        public bool IsInBigPicture()
        {
            return WrappedContext.BigPicturePID > 0;
        }

        public void Init()
        {
            if (!Is32Bit())
            {
                Steam4NETProxy.Init();
            }
            else
            {
                WrappedContext.Init();
            }
        }

        public void Shutdown()
        {
            lock (CoreInstanceMutex)
            {
                if (!Is32Bit())
                {
                    Steam4NETProxy.Dispose();
                    Steam4NETProxy = null;
                }
                else
                {
                    WrappedContext.Shutdown();
                }
                CoreInstance = null;
            }
        }
    }
}
