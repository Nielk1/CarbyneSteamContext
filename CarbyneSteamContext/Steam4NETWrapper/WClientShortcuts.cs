using Steam4NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarbyneSteamContext.Steam4NETWrapper
{
    public class WClientShortcuts
    {
        private IClientShortcuts ClientShortcuts { get; set; }

        public WClientShortcuts(IClientShortcuts clientShortcuts)
        {
            this.ClientShortcuts = clientShortcuts;
        }

        public uint AddOpenVRShortcut(string appName, string exe, string icon, string startDir)
        {
            return ClientShortcuts.AddOpenVRShortcut(appName, exe, icon, startDir);
        }

        public uint AddShortcut(string appName, string exe, string icon, string startDir)
        {
            return ClientShortcuts.AddShortcut(appName, exe, icon, startDir);
        }

        public uint AddTemporaryShortcut(string appName, string exe, string icon, string startDir)
        {
            return ClientShortcuts.AddTemporaryShortcut(appName, exe, icon, startDir);
        }

        public void SetShortcutHidden(uint unAppID, bool arg1)
        {
            ClientShortcuts.SetShortcutHidden(unAppID, arg1);
        }

        public void SetShortcutIcon(uint unAppID, string arg1)
        {
            ClientShortcuts.SetShortcutIcon(unAppID, arg1);
        }

        public void SetAllowDesktopConfig(uint unAppID, bool arg1)
        {
            ClientShortcuts.SetAllowDesktopConfig(unAppID, arg1);
        }

        public void AddShortcutUserTag(uint unAppID, string arg1)
        {
            ClientShortcuts.AddShortcutUserTag(unAppID, arg1);
        }

        public uint GetAppIDForGameID(CGameID gameID)
        {
            return ClientShortcuts.GetAppIDForGameID(gameID);
        }

        public string GetShortcutExeByAppID(uint unAppID)
        {
            return ClientShortcuts.GetShortcutExeByAppID(unAppID);
        }

        public void SetShortcutAppName(uint unAppID, string arg1)
        {
            ClientShortcuts.SetShortcutAppName(unAppID, arg1);
        }

        public uint GetShortcutCount()
        {
            return ClientShortcuts.GetShortcutCount();
        }

        public string GetShortcutExeByIndex(uint uIndex)
        {
            return ClientShortcuts.GetShortcutExeByIndex(uIndex);
        }

        public string GetShortcutAppNameByIndex(uint uIndex)
        {
            return ClientShortcuts.GetShortcutAppNameByIndex(uIndex);
        }

        public string GetShortcutStartDirByAppID(uint unAppID)
        {
            return ClientShortcuts.GetShortcutStartDirByAppID(unAppID);
        }

        public string GetShortcutIconByAppID(uint unAppID)
        {
            return ClientShortcuts.GetShortcutIconByAppID(unAppID);
        }

        public string GetShortcutPathByAppID(uint unAppID)
        {
            return ClientShortcuts.GetShortcutPathByAppID(unAppID);
        }

        public bool BIsShortcutHiddenByAppID(uint unAppID)
        {
            return ClientShortcuts.BIsShortcutHiddenByAppID(unAppID);
        }

        public bool BAllowDesktopConfigByAppID(uint unAppID)
        {
            return ClientShortcuts.BAllowDesktopConfigByAppID(unAppID);
        }

        public bool BIsOpenVRShortcutByIndex(uint uIndex)
        {
            return ClientShortcuts.BIsOpenVRShortcutByIndex(uIndex);
        }

        public uint GetShortcutUserTagCountByIndex(uint uIndex)
        {
            return ClientShortcuts.GetShortcutUserTagCountByIndex(uIndex);
        }

        public string GetShortcutUserTagByIndex(uint uIndex, uint arg1)
        {
            return ClientShortcuts.GetShortcutUserTagByIndex(uIndex, arg1);
        }

        public void RemoveShortcut(uint unAppID)
        {
            ClientShortcuts.RemoveShortcut(unAppID);
        }

        public uint GetShortcutAppIDByIndex(uint unAppID)
        {
            return ClientShortcuts.GetShortcutAppIDByIndex(unAppID);
        }

        public CGameID GetGameIDForAppID(uint unAppID)
        {
            return ClientShortcuts.GetGameIDForAppID(unAppID);
        }

        public string GetShortcutAppNameByAppID(uint unAppID)
        {
            return ClientShortcuts.GetShortcutAppNameByAppID(unAppID);
        }

        public void RemoveAllTemporaryShortcuts()
        {
            ClientShortcuts.RemoveAllTemporaryShortcuts();
        }
    }
}
