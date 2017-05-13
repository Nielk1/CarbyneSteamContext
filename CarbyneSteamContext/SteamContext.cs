using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Data.HashFunction;
using System.Globalization;
using System.IO;
using CarbyneSteamContext.Steam4NETWrapper;
using Gameloop.Vdf;
using CarbyneSteamContext.Models.BVdf;
using CarbyneSteamContext.Models;
using System.Text.RegularExpressions;
using Steam4NET;
using System.Diagnostics;
using System.Threading;
//using HSteamPipe = System.Int32;
//using HSteamUser = System.Int32;

namespace CarbyneSteamContext
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

        public static readonly UInt32[] GoldSrcModHosts = new UInt32[] { 70 };
        public static readonly string[] GoldSrcModSkipFolders = new string[] { "bshift", "bshift_hd", "gearbox", "gearbox_hd", "htmlcache", "platform", "Soundtrack", "valve", "valve_hd" };

        private Int32 Pipe { get; set; }
        private Int32 User { get; set; }

        private ISteamClient017 SteamClient { get; set; }
        private ISteamUser017 SteamUser { get; set; }
        private ISteamAppList001 SteamAppList { get; set; }
        private ISteamApps006 SteamApps { get; set; }

        private WClientEngine ClientEngine { get; set; }
        private WClientShortcuts ClientShortcuts { get; set; }

        private Steam4NET.IClientAppManager ClientAppManager { get; set; }

        /// <summary>
        /// Get Big Picture PID from Registy
        /// </summary>
        /// <value>
        /// Window ID of Big Picture, 0 if not in use
        /// </value>
        /// <remarks>
        /// Same as Steam's PID if in big picture, else 0
        /// </remarks>
        public Int32 BigPicturePID
        {
            get
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
                if (key != null)
                {
                    return (Int32)(key.GetValue("BigPictureInForeground"));
                }
                return 0;
            }
        }
        /// <summary>
        /// Get Steam Process ID
        /// </summary>
        /// <value>
        /// Steam Process ID
        /// </value>
        public Int32 SteamPID
        {
            get
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\ActiveProcess");
                if (key != null)
                {
                    return (Int32)(key.GetValue("pid"));
                }
                return 0;
            }
        }

        /// <summary>
        /// Steam is loaded
        /// </summary>
        public bool IsSteamLoaded { get; private set; }
        /// <summary>
        /// Steam client is loaded
        /// </summary>
        public bool IsSteamClientLoaded { get; private set; }

        /// <summary>
        /// SteamClient Interface Exists
        /// </summary>
        public bool HasSteamClientInterface { get { return SteamClient != null; } }
        /// <summary>
        /// SteamUser Interface Exists
        /// </summary>
        public bool HasSteamUserInterface { get { return SteamUser != null; } }

        /// <summary>
        /// ClientEngine Interface Exists
        /// </summary>
        public bool HasClientEngineInterface { get { return ClientEngine != null; } }
        /// <summary>
        /// ClientShortcuts Interface Exists
        /// </summary>
        public bool HasClientShortcutsInterface { get { return ClientShortcuts != null; } }

        /// <summary>
        /// Current active user
        /// </summary>
        public UInt32 CurrentUserID
        {
            get
            {
                try
                {
                    if (HasClientShortcutsInterface)
                    {
                        Steam4NET.CSteamID id = SteamUser.GetSteamID();
                        string installPath = Steam4NET.Steamworks.GetInstallPath();
                        if (id != null && installPath != null && id.AccountID > 0)
                        {
                            return id.AccountID;
                        }
                    }
                }
                catch { }

                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\ActiveProcess");
                if (key != null)
                {
                    object _possibleValue = key.GetValue("ActiveUser");
                    if (_possibleValue != null)
                    {
                        UInt32 possibleValue = (UInt32)_possibleValue;
                        if (possibleValue > 0)
                        {
                            return possibleValue;
                        }
                    }
                }

                return 0;
            }
        }

        /*public bool CanGetUserShortcutFile
        {
            get
            {
                return IsSteam4NETLoaded && SteamUser != null;
            }
        }*/

        //private Steam4NETProxy Steam4NETProxy;

        private static readonly object CoreInstanceMutex = new object();
        private static SteamContext CoreInstance;
        private SteamContext()
        {
            Init(); // attempt a free init
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

        public static SteamContext GetInstance()
        {
            lock (CoreInstanceMutex)
            {
                if (CoreInstance == null)
                    CoreInstance = new SteamContext();
                return CoreInstance;
            }
        }

        public void Init()
        {
            if (SteamPID == 0)
                return;

            if (Is32Bit())
            {
                if (Steamworks.LoadSteam())
                    IsSteamLoaded = true;

                if (Steamworks.LoadSteamClient())
                    IsSteamClientLoaded = true;
            }

            if (IsSteamLoaded)
            {
                if (SteamClient == null)
                {
                    SteamClient = Steamworks.CreateInterface<ISteamClient017>();
                }

                if (SteamClient != null)
                {
                    #region Pipe
                    if (Pipe == 0)
                    {
                        Pipe = SteamClient.CreateSteamPipe();

                        if (Pipe == 0)
                        {
                            throw new SteamException("Unable to create steam pipe.");
                        }
                    }
                    #endregion Pipe
                    #region User
                    if (User == 0 || User == -1)
                    {
                        User = SteamClient.ConnectToGlobalUser(Pipe);

                        if (User == 0 || User == -1)
                        {
                            throw new SteamException("Unable to connect to global user.");
                        }
                    }
                    #endregion User
                    if (Pipe > 0 && User > 0)
                    {
                        #region SteamUser
                        if (SteamUser == null)
                        {
                            SteamUser = SteamClient.GetISteamUser<Steam4NET.ISteamUser017>(User, Pipe);
                        }
                        #endregion SteamUser
                        #region SteamAppList
                        if (SteamAppList == null)
                        {
                            SteamAppList = SteamClient.GetISteamAppList<Steam4NET.ISteamAppList001>(User, Pipe);
                        }
                        #endregion SteamAppList
                        #region SteamApps
                        if (SteamApps == null)
                        {
                            SteamApps = SteamClient.GetISteamApps<Steam4NET.ISteamApps006>(User, Pipe);
                        }
                        #endregion SteamApps
                    }
                }
            }

            if (IsSteamClientLoaded)
            {
                if (ClientEngine == null)
                {
                    ClientEngine = WClientEngine.Create(); // Steamworks.CreateInterface<IClientEngine>();
                }

                if (ClientEngine != null)
                {
                    if (ClientShortcuts == null)
                    {
                        ClientShortcuts = ClientEngine.GetIClientShortcuts(User, Pipe); // ClientEngine.GetIClientShortcuts<IClientShortcuts>(User, Pipe);
                    }
                    if (ClientAppManager == null)
                    {
                        ClientAppManager = ClientEngine.GetIClientAppManager(User, Pipe);
                    }
                }
            }
        }
        public void Shutdown()
        {
            SteamUser = null;
            if (ClientEngine != null && User != 0 && User != -1)
            {
                ClientEngine.ReleaseUser(Pipe, User);
            }
            if (ClientEngine != null && Pipe != 0)
            {
                ClientEngine.BReleaseSteamPipe(Pipe);
            }

            ClientShortcuts = null;
            ClientAppManager = null;
            if (SteamClient != null && User != 0 && User != -1)
            {
                SteamClient.ReleaseUser(Pipe, User);
            }
            if (SteamClient != null && Pipe != 0)
            {
                SteamClient.BReleaseSteamPipe(Pipe);
            }

        }

        /// <summary>
        /// User Shortcut File
        /// </summary>
        public string GetUserShortcutFile()
        {
            uint userid = CurrentUserID;

            try
            {
                string installPath = Steamworks.GetInstallPath();
                string shortcutFile = Path.Combine(installPath, @"userdata", userid.ToString(), @"config", @"shortcuts.vdf");
                if (File.Exists(shortcutFile))
                    return shortcutFile;
            }
            catch { }

            {
                string installPath = null;
                RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam");
                if (key == null)
                {
                    key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");
                }
                if (key != null)
                {
                    installPath = key.GetValue("InstallPath").ToString();
                }
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
                    if (key != null)
                    {
                        installPath = key.GetValue("SteamPath").ToString();
                    }
                }
                if (installPath == null)
                    return null;

                {
                    string basePath = Path.Combine(installPath, @"userdata");
                    if (userid == 0)
                    {
                        string path = Directory.GetDirectories(basePath).OrderByDescending(dr => new DirectoryInfo(dr).LastAccessTimeUtc).FirstOrDefault();
                        if (path != null)
                        {
                            string shortcutFile = Path.Combine(path, @"config", @"shortcuts.vdf");
                            if (File.Exists(shortcutFile))
                                return shortcutFile;
                        }
                    }
                    {
                        string shortcutFile = Path.Combine(basePath, userid.ToString(), @"config", @"shortcuts.vdf");
                        if (File.Exists(shortcutFile))
                            return shortcutFile;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// User Local Config File
        /// </summary>
        public string GetUserLocalConfigFile()
        {
            uint userid = CurrentUserID;

            try
            {
                string installPath = Steamworks.GetInstallPath();
                string shortcutFile = Path.Combine(installPath, @"userdata", userid.ToString(), @"config", @"localconfig.vdf");
                if (File.Exists(shortcutFile))
                    return shortcutFile;
            }
            catch { }

            {
                string installPath = null;
                RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam");
                if (key == null)
                {
                    key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");
                }
                if (key != null)
                {
                    installPath = key.GetValue("InstallPath").ToString();
                }
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
                    if (key != null)
                    {
                        installPath = key.GetValue("SteamPath").ToString();
                    }
                }
                if (installPath == null)
                    return null;

                {
                    string basePath = Path.Combine(installPath, @"userdata");
                    if (userid == 0)
                    {
                        string path = Directory.GetDirectories(basePath).OrderByDescending(dr => new DirectoryInfo(dr).LastAccessTimeUtc).FirstOrDefault();
                        if (path != null)
                        {
                            string shortcutFile = Path.Combine(path, @"config", @"localconfig.vdf");
                            if (File.Exists(shortcutFile))
                                return shortcutFile;
                        }
                    }
                    {
                        string shortcutFile = Path.Combine(basePath, userid.ToString(), @"config", @"localconfig.vdf");
                        if (File.Exists(shortcutFile))
                            return shortcutFile;
                    }
                }
            }

            return null;
        }

        public string GetAppCacheAppInfoFile()
        {
            try
            {
                string installPath = Steamworks.GetInstallPath();
                string shortcutFile = Path.Combine(installPath, @"appcache", @"appinfo.vdf");
                if (File.Exists(shortcutFile))
                    return shortcutFile;
            }
            catch { }

            {
                string installPath = null;
                RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam");
                if (key == null)
                {
                    key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");
                }
                if (key != null)
                {
                    installPath = key.GetValue("InstallPath").ToString();
                }
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
                    if (key != null)
                    {
                        installPath = key.GetValue("SteamPath").ToString();
                    }
                }
                if (installPath == null)
                    return null;

                {
                    string basePath = Path.Combine(installPath, @"appcache");
                    {
                        string shortcutFile = Path.Combine(basePath, @"appinfo.vdf");
                        if (File.Exists(shortcutFile))
                            return shortcutFile;
                    }
                }
            }

            return null;
        }

        public string GetAppCachePackageInfoFile()
        {
            try
            {
                string installPath = Steamworks.GetInstallPath();
                string shortcutFile = Path.Combine(installPath, @"appcache", @"packageinfo.vdf");
                if (File.Exists(shortcutFile))
                    return shortcutFile;
            }
            catch { }

            {
                string installPath = null;
                RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam");
                if (key == null)
                {
                    key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");
                }
                if (key != null)
                {
                    installPath = key.GetValue("InstallPath").ToString();
                }
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
                    if (key != null)
                    {
                        installPath = key.GetValue("SteamPath").ToString();
                    }
                }
                if (installPath == null)
                    return null;

                {
                    string basePath = Path.Combine(installPath, @"appcache");
                    {
                        string shortcutFile = Path.Combine(basePath, @"packageinfo.vdf");
                        if (File.Exists(shortcutFile))
                            return shortcutFile;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Source Mod path
        /// </summary>
        public string GetSourceModPath()
        {
            try
            {
                string installPath = Steamworks.GetInstallPath();
                return Path.Combine(installPath, @"steamapps", @"sourcemods");
            }
            catch { }

            {
                string installPath = null;
                RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam");
                if (key == null)
                {
                    key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");
                }
                if (key != null)
                {
                    installPath = key.GetValue("InstallPath").ToString();
                }
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam");
                    if (key != null)
                    {
                        installPath = key.GetValue("SteamPath").ToString();
                    }
                }
                if (installPath == null)
                    return null;

                return Path.Combine(installPath, @"steamapps", @"sourcemods");
            }

            return null;
        }

        /*
        public bool AddShortcuts(List<SteamShortcut> shortcuts, string shortcutFilePath)
        {
            if (SteamPID > 0)
            {
                if (IsSteam4NETLoaded && HasClientShortcutsInterface)
                {
                    try
                    {
                        //int counter = 0;
                        shortcuts.ForEach(shortcut =>
                        {
                            uint shortcutid;
                            if (shortcut.OpenVR)
                            {
                                shortcutid = ClientShortcuts.AddOpenVRShortcut(shortcut.appname, shortcut.exe, shortcut.icon, shortcut.ShortcutPath);
                            }
                            else
                            {
                                shortcutid = ClientShortcuts.AddShortcut(
                                    shortcut.appname,
                                    shortcut.exe.Trim('"'), // system adds quotes automaticly
                                    shortcut.icon.Trim('"'), // system adds quotes automaticly
                                    shortcut.ShortcutPath.Trim('"'));
                            }
                            ClientShortcuts.SetShortcutHidden(shortcutid, shortcut.hidden);
                            ClientShortcuts.SetShortcutIcon(shortcutid, shortcut.icon);
                            ClientShortcuts.SetAllowDesktopConfig(shortcutid, shortcut.AllowDesktopConfig);
                            shortcut.tags.ForEach(tag => ClientShortcuts.AddShortcutUserTag(shortcutid, tag));
                            //counter++;

                            //if(counter % 200 == 0)
                            //{
                            //    Thread.Sleep(1000);
                            //}
                        });
                        return true;
                    }
                    catch
                    {
                        throw new SteamException("Error Adding Shortcut");
                        //return false;
                    }
                }
                else
                {
                    return false; // can't access API, can't edit file as Steam is open
                }
            }
            else
            {
                if (shortcutFilePath == null)
                    return false;

                VPropertyCollection ShortcutFile = SteamShortcutDataFile.Read(shortcutFilePath);

                VPropertyCollection shortcutVData = (VPropertyCollection)ShortcutFile["shortcuts"];

                shortcuts.Distinct().ToList().ForEach(shortcut =>
                {
                    VPropertyCollection shortcutData = new VPropertyCollection();
                    shortcutData["appname"] = shortcut.appname; // name
                    shortcutData["exe"] = shortcut.exe; // full path
                    shortcutData["StartDir"] = shortcut.StartDir; // working folder
                    shortcutData["icon"] = shortcut.icon; // icon path
                    shortcutData["ShortcutPath"] = shortcut.ShortcutPath; // empty
                    shortcutData["IsHidden"] = shortcut.hidden ? 1 : 0; // 
                    shortcutData["AllowDesktopConfig"] = shortcut.AllowDesktopConfig ? 1 : 0; // 
                    shortcutData["OpenVR"] = shortcut.OpenVR ? 1 : 0; // 

                    VPropertyCollection tagData = new VPropertyCollection();
                    shortcut.tags.ForEach(tag => tagData.Add(tag));

                    shortcutData["tags"] = tagData; // 

                    shortcutVData.Add(shortcutData);
                });

                File.Copy(shortcutFilePath, shortcutFilePath + DateTime.UtcNow.ToString(".yyyyMMddHHmmss"));
                SteamShortcutDataFile.Write(shortcutFilePath, ShortcutFile);
                return true;
            }
        }
        */

        public uint AddTemporaryShortcut(SteamLaunchableShortcut shortcut)//, string shortcutFilePath)
        {
            if (SteamPID > 0)
            {
                if (HasClientShortcutsInterface)
                {
                    try
                    {
                        uint shortcutAppId = ClientShortcuts.AddTemporaryShortcut(
                                shortcut.appname,
                                shortcut.exe.Trim('"'), // system adds quotes automaticly
                                shortcut.icon.Trim('"'), // system adds quotes automaticly
                                shortcut.ShortcutPath.Trim('"'));
                        ClientShortcuts.SetShortcutHidden(shortcutAppId, shortcut.hidden);
                        ClientShortcuts.SetShortcutIcon(shortcutAppId, shortcut.icon);
                        ClientShortcuts.SetAllowDesktopConfig(shortcutAppId, shortcut.AllowDesktopConfig);
                        shortcut.tags.ForEach(tag => ClientShortcuts.AddShortcutUserTag(shortcutAppId, tag));

                        return shortcutAppId;
                    }
                    catch
                    {
                        throw new SteamException("Error Adding Shortcut");
                    }
                }
            }
            return 0;
        }

        public void RemoveAllTemporaryShortcuts()
        {
            if (SteamPID > 0)
            {
                try
                {
                    ClientShortcuts.RemoveAllTemporaryShortcuts();
                }
                catch
                {
                    throw new SteamException("Error Removing Shortcuts");
                }
            }
        }

        /*
        public UInt64 RenameLiveShortcut(UInt64 shortcutID, string name)
        {
            if (SteamPID > 0)
            {
                if (IsSteam4NETLoaded && HasClientShortcutsInterface)
                {
                    try
                    {
                        UInt32 appID = ClientShortcuts.GetAppIDForGameID(new CGameID(shortcutID));
                        if (appID > 0)
                        {
                            string exe = ClientShortcuts.GetShortcutExeByAppID(appID);
                            ClientShortcuts.SetShortcutAppName(appID, name);
                            return SteamShortcut.GetShortcutID(exe, name);
                        }
                    }
                    catch { }
                }
            }
            return 0;
        }
        */

        public UInt64 GetShortcutID(string appname, string exe, string shortcutFilePath)
        {
            if (SteamPID > 0)
            {
                if (HasClientShortcutsInterface)
                {
                    try
                    {
                        uint CountShortcuts = ClientShortcuts.GetShortcutCount();
                        for (uint x = 0; x < CountShortcuts; x++)
                        {
                            string steamexe = ClientShortcuts.GetShortcutExeByIndex(x).Trim('"');
                            string steamappname = ClientShortcuts.GetShortcutAppNameByIndex(x);

                            if (exe == steamexe && appname == steamappname)
                            {
                                SteamLaunchableShortcut shortcutData = SteamLaunchableShortcut.Make(ClientShortcuts.GetShortcutExeByIndex(x), appname);
                                if (shortcutData != null)
                                {
                                    UInt64 shortcutID = shortcutData.GetShortcutID();
                                    UInt32 appID = ClientShortcuts.GetAppIDForGameID(new Steam4NET.CGameID(shortcutID));
                                    if (appID > 0)
                                    {
                                        return shortcutID;
                                    }
                                }
                            }
                        }
                        //return 0;
                    }
                    catch { }
                }
            }
            {
                if (shortcutFilePath == null)
                    return 0;

                BVPropertyCollection ShortcutFile = SteamShortcutDataFile.Read(shortcutFilePath);

                BVPropertyCollection shortcutVData = (BVPropertyCollection)ShortcutFile["shortcuts"];

                foreach (BVProperty _shortcutData in shortcutVData.Properties)
                {
                    BVPropertyCollection shortcutData = (BVPropertyCollection)_shortcutData.Value;
                    if (exe == ((BVStringToken)shortcutData["exe"]).Value.Trim('"')
                      && appname == ((BVStringToken)shortcutData["appname"]).Value)
                    {
                        SteamLaunchableShortcut shortcutDataX = SteamLaunchableShortcut.Make(exe, appname);
                        if (shortcutDataX != null)
                        {
                            return shortcutDataX.GetShortcutID();
                        }
                    }
                }

                return 0;
            }
        }

        /*
        public List<SteamShortcut> GetShortcutsForExe(string exe, string shortcutFilePath)
        {
            List<SteamShortcut> shortcuts = new List<SteamShortcut>();

            if (SteamPID > 0)
            {
                if (IsSteam4NETLoaded && HasClientShortcutsInterface)
                {
                    try
                    {
                        uint CountShortcuts = ClientShortcuts.GetShortcutCount();
                        for (uint x = 0; x < CountShortcuts; x++)
                        {
                            if (exe == ClientShortcuts.GetShortcutExeByIndex(x).Trim('"'))
                            {
                                string appname = ClientShortcuts.GetShortcutAppNameByIndex(x);
                                UInt32 appID = ClientShortcuts.GetAppIDForGameID(new CGameID(SteamShortcut.GetShortcutID(ClientShortcuts.GetShortcutExeByIndex(x), appname)));
                                if (appID > 0)
                                {
                                    string StartDir = ClientShortcuts.GetShortcutStartDirByAppID(appID);
                                    string icon = ClientShortcuts.GetShortcutIconByAppID(appID);
                                    string ShortcutPath = ClientShortcuts.GetShortcutPathByAppID(appID);
                                    bool hidden = ClientShortcuts.BIsShortcutHiddenByAppID(appID);
                                    bool AllowDesktopConfig = ClientShortcuts.BAllowDesktopConfigByAppID(appID);
                                    bool OpenVR = ClientShortcuts.BIsOpenVRShortcutByIndex(x);
                                    List<string> tags = new List<string>();

                                    uint CountTags = ClientShortcuts.GetShortcutUserTagCountByIndex(x);
                                    for (uint y = 0; y < CountTags; y++)
                                    {
                                        tags.Add(ClientShortcuts.GetShortcutUserTagByIndex(x, y));
                                    }

                                    shortcuts.Add(new SteamShortcut(appname, exe, StartDir, icon, ShortcutPath, hidden, AllowDesktopConfig, OpenVR, tags));
                                }
                            }
                        }
                        return shortcuts;
                    }
                    catch
                    {
                        throw new SteamException("Error Finding Shortcuts");
                        //return null;
                    }
                }
                else
                {
                    return null; // can't access API, can't edit file as Steam is open
                }
            }
            else
            {
                if (shortcutFilePath == null)
                    return null;

                VPropertyCollection ShortcutFile = SteamShortcutDataFile.Read(shortcutFilePath);

                VPropertyCollection shortcutVData = (VPropertyCollection)ShortcutFile["shortcuts"];

                shortcutVData.Properties.ForEach(_shortcutData =>
                {
                    VPropertyCollection shortcutData = (VPropertyCollection)_shortcutData.Value;
                    if (exe == ((VStringToken)shortcutData["exe"]).Value.Trim('"'))
                    {
                        string appname = ((VStringToken)shortcutData["appname"]).Value;
                        //string exe = ((VStringToken)shortcutData["exe"]).Value;
                        string StartDir = ((VStringToken)shortcutData["StartDir"]).Value;
                        string icon = ((VStringToken)shortcutData["icon"]).Value;
                        string ShortcutPath = ((VStringToken)shortcutData["ShortcutPath"]).Value;
                        bool hidden = ((VIntToken)shortcutData["IsHidden"]).Value > 0;
                        bool AllowDesktopConfig = ((VIntToken)shortcutData["AllowDesktopConfig"]).Value > 0;
                        bool OpenVR = ((VIntToken)shortcutData["OpenVR"]).Value > 0;
                        List<string> tags = new List<string>();

                        VPropertyCollection tagData = ((VPropertyCollection)shortcutData["tags"]);
                        tagData.Properties.ForEach(dr => tags.Add(((VStringToken)dr.Value).Value));

                        shortcuts.Add(new SteamShortcut(appname, exe, StartDir, icon, ShortcutPath, hidden, AllowDesktopConfig, OpenVR, tags));
                    }
                });

                return shortcuts;
            }
        }

        /// <summary>
        /// Remove shortcuts by matching the exe and appname
        /// </summary>
        /// <param name="shortcuts"></param>
        /// <param name="shortcutFilePath"></param>
        /// <returns>Count removed shortcuts</returns>
        public int RemoveShortcuts(List<SteamShortcut> shortcuts, string shortcutFilePath)
        {
            int count = 0;
            if (SteamPID > 0)
            {
                if (IsSteam4NETLoaded && HasClientShortcutsInterface)
                {
                    try
                    {
                        shortcuts.ForEach(shortcut =>
                        {
                            UInt32 appid = ClientShortcuts.GetAppIDForGameID(new CGameID(shortcut.GetShortcutID()));
                            if (appid > 0)
                            {
                                ClientShortcuts.RemoveShortcut(appid);
                            }
                            //if (count % 200 == 0)
                            //{
                            //    Thread.Sleep(1000);
                            //}
                        });
                        count++;
                        return count;
                    }
                    catch
                    {
                        throw new SteamException("Error Removing Shortcut");
                        //return 0;
                    }
                }
                else
                {
                    return 0; // can't access API, can't edit file as Steam is open
                }
            }
            else
            {
                if (shortcutFilePath == null)
                    return 0;

                VPropertyCollection ShortcutFile = SteamShortcutDataFile.Read(shortcutFilePath);

                VPropertyCollection shortcutVData = (VPropertyCollection)ShortcutFile["shortcuts"];

                List<string> idsToRemove = new List<string>();
                shortcutVData.Properties.ForEach(dr =>
                {
                    VPropertyCollection shortcutData = (VPropertyCollection)dr.Value;
                    shortcuts.Distinct().ToList().ForEach(shortcut =>
                    {
                        if ((((VStringToken)shortcutData["appname"]).Value + ((VStringToken)shortcutData["exe"]).Value)
                          == (shortcut.appname + shortcut.exe))
                        {
                            idsToRemove.Add(dr.Key);
                        }
                    });
                });

                idsToRemove.OrderByDescending(dr => int.Parse(dr)).ToList().ForEach(dr =>
                {
                    shortcutVData.Remove(dr);
                    count++;
                });

                File.Copy(shortcutFilePath, shortcutFilePath + DateTime.UtcNow.ToString(".yyyyMMddHHmmss"));
                File.Delete(shortcutFilePath);
                //File.Create(shortcutFilePath);
                SteamShortcutDataFile.Write(shortcutFilePath, ShortcutFile);

                return count;
            }
        }
        */




        public uint[] GetClientAppIds()
        {
            uint[] localAppIDsAppIDs = null;
            string localconfig = GetUserLocalConfigFile();
            {
                Gameloop.Vdf.VObject obj = VdfConvert.Deserialize(File.ReadAllText(localconfig), new VdfSerializerSettings() { UsesEscapeSequences = true });
                VObject UserLocalConfigStore = (Gameloop.Vdf.VObject)obj["UserLocalConfigStore"];
                VObject appTickets = (Gameloop.Vdf.VObject)UserLocalConfigStore["apptickets"];
                localAppIDsAppIDs = appTickets
                    .Children()
                    .Select(dr =>
                    {
                        uint tmp = 0;
                        if (uint.TryParse(dr.Key, out tmp))
                        {
                            return (uint?)tmp;
                        }
                        return null;
                    }).Where(dr => dr != null).Select(dr => dr.Value).ToArray();
            }

            uint[] installedAppIDs = null;
            if (ClientAppManager != null)
            {
                uint InstalledAppIDCount = ClientAppManager.GetNumInstalledApps();
                installedAppIDs = new uint[InstalledAppIDCount];
                ClientAppManager.GetInstalledApps(ref installedAppIDs[0], InstalledAppIDCount);
            }
            else
            {
                installedAppIDs = new uint[0];
            }

            return localAppIDsAppIDs.Union(installedAppIDs).OrderBy(dr => dr).ToArray();
        }

        public SteamAppInfoDataFile GetSteamAppInfoDataFile(string path = null)
        {
            return SteamAppInfoDataFile.Read(path ?? GetAppCacheAppInfoFile());
        }

        public SteamPackageInfoDataFile GetSteamPackageInfoDataFile(string path = null)
        {
            return SteamPackageInfoDataFile.Read(path ?? GetAppCachePackageInfoFile());
        }

        // TODO use a better AppID source, the AppInfo.vtf file only holds data the client has actually seen
        public List<SteamLaunchableApp> GetClientApps()
        {
            UInt32[] appIDs = GetClientAppIds();
            List<SteamLaunchableApp> apps = new List<SteamLaunchableApp>();

            //SteamAppInfoDataFile dataFile = SteamAppInfoDataFile.Read(GetAppCacheAppInfoFile());
            SteamAppInfoDataFile dataFile = GetSteamAppInfoDataFile();

            dataFile.chunks
                .Where(chunk => chunk.data != null && chunk.data.Properties != null && chunk.data.Properties.Count > 0)
                //.Select(chunk => ((BVStringToken)((BVPropertyCollection)((BVPropertyCollection)chunk.data?["appinfo"])?["common"])?["type"])?.Value?.ToLowerInvariant())
                .Select(chunk => ((BVStringToken)((BVPropertyCollection)((BVPropertyCollection)chunk.data?["appinfo"])?["common"])?["releasestate"])?.Value?.ToLowerInvariant())
                .Distinct()
                .OrderBy(dr => dr)
                .ToList()
                .ForEach(dr =>
                {
                    Console.WriteLine(dr);
                });

            dataFile.chunks
                .ForEach(chunk =>
                {
                    if (chunk.data != null
                    && chunk.data.Properties != null
                    && chunk.data.Properties.Count > 0)
                    {
                        BVPropertyCollection appinfo = ((BVPropertyCollection)chunk.data?["appinfo"]);
                        BVPropertyCollection common = ((BVPropertyCollection)appinfo?["common"]);
                        BVPropertyCollection extended = ((BVPropertyCollection)appinfo?["extended"]);

                        string type = common?["type"]?.GetValue<string>()?.ToLowerInvariant();
                        if (type == "demo" || type == "game")
                        {
                            bool isInstalled = SteamApps.BIsAppInstalled(chunk.AppID);
                            bool isSubscribed = SteamApps.BIsSubscribedApp(chunk.AppID);

                            string name = common?["name"]?.GetValue<string>();
                            string oslist = common?["oslist"]?.GetValue<string>();
                            string icon = common?["icon"]?.GetValue<string>();
                            string clienttga = common?["clienttga"]?.GetValue<string>();
                            string clienticon = common?["clienticon"]?.GetValue<string>();
                            string logo = common?["logo"]?.GetValue<string>();
                            string logo_small = common?["logo_small"]?.GetValue<string>();
                            string releasestate = common?["releasestate"]?.GetValue<string>();
                            string linuxclienticon = common?["linuxclienticon"]?.GetValue<string>();
                            string controller_support = common?["controller_support"]?.GetValue<string>();
                            string clienticns = common?["clienticns"]?.GetValue<string>();
                            int metacritic_score = ((BVInt32Token)common?["metacritic_score"])?.Value ?? -1;
                            string metacritic_name = common?["metacritic_name"]?.GetValue<string>();
                            BVPropertyCollection small_capsule = ((BVPropertyCollection)common?["small_capsule"]);
                            BVPropertyCollection header_image = ((BVPropertyCollection)common?["header_image"]);
                            BVPropertyCollection languages = ((BVPropertyCollection)common?["languages"]);
                            bool community_visible_stats = common?["community_visible_stats"]?.GetValue<string>() == "1";
                            bool community_hub_visible = common?["community_hub_visible"]?.GetValue<string>() == "1";
                            bool workshop_visible = common?["workshop_visible"]?.GetValue<string>() == "1";
                            bool exfgls = common?["exfgls"]?.GetValue<string>() == "1";
                            string gamedir = extended?["gamedir"]?.GetValue<string>();
                            string developer = extended?["developer"]?.GetValue<string>();
                            string publisher = extended?["publisher"]?.GetValue<string>();
                            string homepage = extended?["homepage"]?.GetValue<string>();
                            string gamemanualurl = extended?["gamemanualurl"]?.GetValue<string>();
                            bool showcdkeyonlaunch = extended?["showcdkeyonlaunch"]?.GetValue<string>() == "1";
                            bool dlcavailableonstore = extended?["dlcavailableonstore"]?.GetValue<string>() == "1";

                            Console.WriteLine($"{chunk.AppID}\t{(type ?? string.Empty).PadRight(4)} {(isInstalled ? 1 : 0)} {(isSubscribed ? 1 : 0)} {(releasestate ?? string.Empty).PadRight(11)} {(name ?? string.Empty).PadRight(90)} {(developer ?? string.Empty).PadRight(40)} {(publisher ?? string.Empty)}");
                            //File.AppendAllText("SteamDump.txt",$"{chunk.appID}\t{(type ?? string.Empty).PadRight(4)} {(isInstalled ? 1 : 0)} {(isSubscribed ? 1 : 0)} {(releasestate ?? string.Empty).PadRight(11)} {(name ?? string.Empty).PadRight(90)} {(developer ?? string.Empty).PadRight(40)} {(publisher ?? string.Empty).PadRight(40)}\r\n");
                        }
                    }
                });

            /*dataFile.chunks.Select(chunk => chunk.appType).Distinct().ToList().ForEach(chunk =>
            {
                Console.WriteLine(chunk);
            });*/

            /*foreach (UInt32 appID in appIDs)
            {
                //SteamLaunchableApp app = new SteamLaunchableApp(appID);

                //bool Installed = SteamApps.BIsAppInstalled(app.AppID);
                //bool Subscribed = SteamApps.BIsSubscribedApp(app.AppID);
                //DateTime FirstPay = DateTimeOffset.FromUnixTimeSeconds(SteamApps.GetEarliestPurchaseUnixTime(app.AppID)).UtcDateTime;
                //string Name = null;
                //string InstallPath1 = null;
                //string InstallPath2 = null;

                //StringBuilder tmpBuilder = new StringBuilder(1024);
                //tmpBuilder.Clear();
                //if (SteamAppList.GetAppName(app.AppID, tmpBuilder) > 0)
                //{
                //    Name = tmpBuilder.ToString();
                //}
                //tmpBuilder.Clear();
                //if (SteamApps.GetAppInstallDir(app.AppID, tmpBuilder) > 0)
                //{
                //    InstallPath1 = tmpBuilder.ToString();
                //}
                //tmpBuilder.Clear();
                //if (SteamAppList.GetAppInstallDir(app.AppID, tmpBuilder) > 0)
                //{
                //    InstallPath2 = tmpBuilder.ToString();
                //}
                //tmpBuilder.Clear();




                if (app != null) apps.Add(app);
            }*/

            return apps;
        }

        public List<SteamLaunchableModGoldSrc> GetGoldSrcMods()
        {
            // gold source mods
            List<SteamLaunchableModGoldSrc> GoldSourceMods = new List<SteamLaunchableModGoldSrc>();
            foreach (UInt32 sourceModAppID in GoldSrcModHosts)
            {
                StringBuilder AppInstallDir = new StringBuilder(1024);
                if (SteamApps.GetAppInstallDir(sourceModAppID, AppInstallDir) > 0)
                {
                    if (Directory.Exists(AppInstallDir.ToString()))
                    {
                        Directory.GetDirectories(AppInstallDir.ToString())
                            .Where(dr => !GoldSrcModSkipFolders.Contains(Path.GetFileName(dr)))
                            .Where(dr => File.Exists(Path.Combine(dr, "liblist.gam")))
                            .ToList().ForEach(dr =>
                            {
                                //VObject rootObj = VdfConvert.Deserialize("\"GameInfo\"\r\n{\r\n" + File.ReadAllText(Path.Combine(dr, "liblist.gam")) + "\r\n}");
                                //VObject tmp = (VObject)rootObj["GameInfo"];
                                //VToken tmp2 = tmp["gamedir"];
                                //UInt64 tmpID = SteamLaunchableShortcut.GetModShortcutID(tmp2.ToString(), sourceModAppID);
                                //if (tmpID > 0) GoldSourceMods.Add(tmpID);

                                SteamLaunchableModGoldSrc mod = SteamLaunchableModGoldSrc.Make(sourceModAppID, dr, Path.Combine(dr, "liblist.gam"));
                                if (mod != null) GoldSourceMods.Add(mod);
                            });
                    }
                }
            }
            return GoldSourceMods;
        }

        public List<SteamLaunchableModSource> GetSourceMods()
        {
            // source mods
            List<SteamLaunchableModSource> SourceMods = new List<SteamLaunchableModSource>();
            {
                string sourceMods = GetSourceModPath();
                if (Directory.Exists(sourceMods))
                {
                    Directory.GetDirectories(sourceMods)
                        .Where(dr => File.Exists(Path.Combine(dr, "gameinfo.txt")))
                        .ToList().ForEach(dr =>
                        {
                            VObject rootObj = VdfConvert.Deserialize(File.ReadAllText(Path.Combine(dr, "gameinfo.txt")));
                            VObject GameInfoObj = (VObject)rootObj["GameInfo"];
                            VObject FileSystemObj = (VObject)GameInfoObj["FileSystem"];
                            VToken appID = FileSystemObj["SteamAppId"];

                            UInt32 appIdCheck = 0;
                            if (!UInt32.TryParse(appID.ToString(), out appIdCheck)) return;
                            if (appIdCheck == 0) return;

                            StringBuilder AppInstallDir = new StringBuilder(255);
                            if (SteamApps.GetAppInstallDir(appIdCheck, AppInstallDir) > 0)
                            {
                                SteamLaunchableModSource mod = SteamLaunchableModSource.Make(appIdCheck, Path.GetFileName(dr), rootObj);
                                if (mod != null) SourceMods.Add(mod);
                            }
                        });
                }
            }
            return SourceMods;
        }

        public bool IsInstalled(UInt64 GameID)
        {
            CGameID gameID = new CGameID(GameID);
            switch (gameID.AppType)
            {
                // Basic Steam App
                case CGameID.EGameID.k_EGameIDTypeApp:
                    return SteamApps.BIsAppInstalled(gameID.AppID);
                    //return SteamApps.BIsAppInstalled(gameID.AppID().m_AppId);

                // Mod Steam App
                case CGameID.EGameID.k_EGameIDTypeGameMod:
                    // If the base game isn't installed, just say no
                    if (!SteamApps.BIsAppInstalled(gameID.AppID)) return false;
                    //if (!SteamApps.BIsAppInstalled(gameID.AppID().m_AppId)) return false;

                    // Root app is GoldSrc
                    //if (GoldSrcModHosts.Contains(gameID.AppID().m_AppId))
                    {
                        // Get a list of known GoldSrc Mods
                        List<SteamLaunchableModGoldSrc> mods = GetGoldSrcMods();

                        // return if any of these mods match our ID
                        return mods.Any(dr => dr.GetShortcutID() == GameID);
                    }

                    // Root app is Source
                    // TODO add check for source engine IDs here
                    {
                        // Get a list of known GoldSrc Mods
                        List<SteamLaunchableModSource> mods = GetSourceMods();

                        // return if any of these mods match our ID
                        return mods.Any(dr => dr.GetShortcutID() == GameID);
                    }

                    return false;
                case CGameID.EGameID.k_EGameIDTypeShortcut:
                    break;
            }

            return false;
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

        /*
        public void SetShortcutName(UInt64 shortcutID, string steamShortcutID, string newName)
        {
            if (!IsSteam4NETLoaded)
            {
                throw new SteamException("Unable to load Steam4NET library.");
            }

            try
            {
                uint shortCutCount = ClientShortcuts.GetShortcutCount();
                for (uint index = 0; index < shortCutCount; index++)
                {
                    uint appID = ClientShortcuts.GetShortcutAppIDByIndex(index);

                    CGameID gameID = ClientShortcuts.GetGameIDForAppID(appID);
                    if (shortcutID == gameID.ConvertToUint64()
                      || ClientShortcuts.GetShortcutExeByIndex(index).Contains($"-steamproxyactivate {steamShortcutID}"))
                    {
                        string name = ClientShortcuts.GetShortcutAppNameByAppID(appID);
                        ClientShortcuts.SetShortcutAppName(appID, newName);
                        return;
                    }
                }
            }
            catch { }
        }

        public string GetShortcutName(UInt64 shortcutID, string steamShortcutID)
        {
            if (!IsSteam4NETLoaded)
            {
                throw new SteamException("Unable to load Steam4NET library.");
            }

            try
            {
                uint shortCutCount = ClientShortcuts.GetShortcutCount();
                for (uint index = 0; index < shortCutCount; index++)
                {
                    uint appID = ClientShortcuts.GetShortcutAppIDByIndex(index);

                    CGameID gameID = ClientShortcuts.GetGameIDForAppID(appID);
                    if (shortcutID == gameID.ConvertToUint64()
                      || ClientShortcuts.GetShortcutExeByIndex(index).EndsWith($"-steamproxyactivate {steamShortcutID}"))
                    {
                        return ClientShortcuts.GetShortcutAppNameByAppID(appID);
                    }
                }
            }
            catch { }
            return null;
        }

        public UInt64 FindShortcut(UInt64 shortcutID, string steamShortcutID)
        {
            if (!IsSteam4NETLoaded)
            {
                throw new SteamException("Unable to load Steam4NET library.");
            }

            try
            {
                uint shortCutCount = ClientShortcuts.GetShortcutCount();
                for (uint index = 0; index < shortCutCount; index++)
                {
                    uint appID = ClientShortcuts.GetShortcutAppIDByIndex(index);

                    CGameID gameID = ClientShortcuts.GetGameIDForAppID(appID);
                    if (shortcutID == gameID.ConvertToUint64()
                      || ClientShortcuts.GetShortcutExeByIndex(index).EndsWith($"-steamproxyactivate {steamShortcutID}"))
                    {
                        return gameID.ConvertToUint64();
                    }
                }
            }
            catch { }
            return 0;
        }
        */
    }


    public abstract class SteamLaunchable
    {
        public enum SteamLaunchableType
        {
            App = 0,
            GameMod = 1,
            Shortcut = 2,
            P2P = 3
        }

        public UInt32 AppID { get; set; }
        public abstract SteamLaunchableType ShortcutType { get; }
        //public string ShortcutData { get; set; }

        public abstract string Title { get; }

        private static readonly CRC.Setting crcSetting = new CRC.Setting(32, 0x04C11DB7, 0xffffffff, true, true, 0xffffffff);

        public abstract string ModIdString { get; }

        public virtual UInt32 GenerateModID()
        {
            CRC algorithm = new CRC(crcSetting);
            string crc_input = ModIdString;
            return BitConverter.ToUInt32(algorithm.ComputeHash(Encoding.UTF8.GetBytes(crc_input).Select(dr => (byte)dr).ToArray()), 0) | 0x80000000;
        }

        public UInt64 GetShortcutID()
        {
            UInt64 high_32 = (((UInt64)GenerateModID()));
            UInt64 full_64 = ((high_32 << 32) | ((UInt32)ShortcutType << 24) | AppID);
            return full_64;
        }
    }

    public abstract class SteamLaunchableMod : SteamLaunchable
    {
        public override SteamLaunchableType ShortcutType { get { return SteamLaunchableType.GameMod; } }
    }

    public class SteamLaunchableModGoldSrc : SteamLaunchableMod
    {
        public Dictionary<string, string> ConfigLines { get; set; }
        public string ModFolder { get; set; }
        public string ModPath { get; set; }
        private string ModTitle { get; set; }

        public override string Title { get { return ModTitle; } }
        public override string ModIdString { get { return ModFolder; } }

        public SteamLaunchableModGoldSrc(uint AppID, string ModFolder, string ModTitle)
        {
            this.AppID = AppID;
            this.ModFolder = ModFolder;
            this.ModTitle = ModTitle;
            this.ConfigLines = new Dictionary<string, string>();
        }

        private static Dictionary<string, string> ProcLibListGamFile(string liblistFilePath)
        {
            string lines = File.ReadAllText(liblistFilePath);
            Dictionary<string, string> ConfigLines = new Dictionary<string, string>();
            var matches = Regex.Matches(lines, "\\s*(\\w+)\\s+\\\"(.*)\\\"", RegexOptions.IgnoreCase & RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value.ToLowerInvariant();
                string value = match.Groups[2].Value;
                if (!ConfigLines.ContainsKey(key)) ConfigLines.Add(key, value);
            }
            return ConfigLines;
        }

        public static SteamLaunchableModGoldSrc Make(UInt32 HostAppID, string ModPath, string liblistFilePath)
        {
            Dictionary<string, string> ConfigLines = ProcLibListGamFile(liblistFilePath);

            string gameDir = ConfigLines.ContainsKey("gamedir") ? ConfigLines["gamedir"] : Path.GetFileName(ModPath);
            string gameTitle = ConfigLines.ContainsKey("game") ? ConfigLines["game"] : gameDir;
            SteamLaunchableModGoldSrc src = new SteamLaunchableModGoldSrc(HostAppID, gameDir, gameTitle)
            {
                ConfigLines = ConfigLines,
                ModPath = ModPath
            };

            return src;
        }
    }

    public class SteamLaunchableModSource : SteamLaunchableMod
    {
        public string ModFolder { get; set; }
        private string ModTitle { get; set; }

        public override string Title { get { return ModTitle; } }
        public override string ModIdString { get { return ModFolder; } }

        public SteamLaunchableModSource(uint HostAppID, string ModFolder, string ModTitle)
        {
            this.AppID = HostAppID;
            this.ModFolder = ModFolder;
            this.ModTitle = ModTitle;
        }

        public static SteamLaunchableModSource Make(UInt32 HostAppID, string ModDir, VObject gameInfo = null)
        {
            VObject GameInfoObj = (VObject)(gameInfo.ContainsKey("GameInfo") ? gameInfo["GameInfo"] : null);
            VToken GameObj = GameInfoObj != null ? GameInfoObj.ContainsKey("game") ? GameInfoObj["game"] : null : null;
            VToken IconObj = GameInfoObj != null ? GameInfoObj.ContainsKey("icon") ? GameInfoObj["icon"] : null : null;

            SteamLaunchableModSource src = new SteamLaunchableModSource(HostAppID, ModDir, GameObj?.ToString())
            {

            };

            return src;
        }
    }

    public class SteamLaunchableApp : SteamLaunchable
    {
        public override string Title { get { return "App"; } }

        public override string ModIdString { get { return string.Empty; } }

        public override SteamLaunchableType ShortcutType { get { return SteamLaunchableType.App; } }

        // hopefully the base class will use this because the base is virtual rather than not
        public override UInt32 GenerateModID()
        {
            return 0 | 0x80000000;
        }

        public SteamLaunchableApp(uint AppID)
        {
            this.AppID = AppID;
        }

        public static SteamLaunchableApp Make(UInt32 AppID)
        {
            SteamLaunchableApp src = new SteamLaunchableApp(AppID)
            {

            };

            return src;
        }
    }

    public class SteamLaunchableShortcut : SteamLaunchable
    {
        public override string Title { get { return "Shortcut"; } }

        public override SteamLaunchableType ShortcutType { get { return SteamLaunchableType.Shortcut; } }

        //"\""
        private string _exe;
        private string _StartDir;
        private string _icon;

        // Minimal for generating ID
        public string appname { get; set; }
        public string exe { get { return "\"" + _exe.Trim('"') + "\""; } set { _exe = value; } }


        public string StartDir { get { return "\"" + _StartDir.Trim('"') + "\""; } set { _StartDir = value; } }
        public string icon { get { return string.IsNullOrEmpty(_icon) ? null : "\"" + _icon.Trim('"') + "\""; } set { _icon = value; } }
        public string ShortcutPath { get; set; }
        public bool hidden { get; set; }
        public bool AllowDesktopConfig { get; set; }
        public bool OpenVR { get; set; }
        public List<string> tags { get; private set; }

        public override string ModIdString { get { return exe + appname; } }

        public SteamLaunchableShortcut(string appname, string exe, string StartDir, string icon = null, string ShortcutPath = null, bool hidden = false, bool AllowDesktopConfig = true, bool OpenVR = false, List<string> tags = null)
        {
            this.appname = appname;
            this.exe = exe;
            this.StartDir = StartDir;
            this.icon = icon;
            this.ShortcutPath = ShortcutPath;
            this.hidden = hidden;
            this.AllowDesktopConfig = AllowDesktopConfig;
            this.OpenVR = OpenVR;
            this.tags = tags ?? new List<string>();
        }

        public SteamLaunchableShortcut(string appname, string exe)
        {
            this.appname = appname;
            this.exe = exe;
        }

        public static SteamLaunchableShortcut Make(string exe, string appname)
        {
            return new SteamLaunchableShortcut(appname, exe);
        }
    }
}
