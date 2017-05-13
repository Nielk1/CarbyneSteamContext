using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarbyneSteamContext.Steam4NETWrapper
{
    public class WClientEngine
    {
        private Steam4NET.IClientEngine ClientEngine { get; set; }
        private Steam4NET.IClientShortcuts ClientShortcuts { get; set; }
        private WClientShortcuts WClientShortcuts { get; set; }

        // temporary
        public IntPtr Interface { get { return ClientEngine.Interface; } }

        private WClientEngine(Steam4NET.IClientEngine ClientEngine)
        {
            this.ClientEngine = ClientEngine;
        }

        public static WClientEngine Create()
        {
            try
            {
                Steam4NET.IClientEngine ClientEngine = Steam4NET.Steamworks.CreateInterface<Steam4NET.IClientEngine>();
                if (ClientEngine == null) return null;
                return new WClientEngine(ClientEngine);
            }
            catch { }
            return null;
        }

        public WClientShortcuts GetIClientShortcuts(int User, int Pipe)
        {
            if (WClientShortcuts != null)
                return WClientShortcuts;

            if (ClientShortcuts == null)
                ClientShortcuts = ClientEngine.GetIClientShortcuts<Steam4NET.IClientShortcuts>(User, Pipe);

            if (ClientShortcuts == null)
                return null;

            WClientShortcuts = new WClientShortcuts(ClientShortcuts);
            return WClientShortcuts;
        }

        public void ReleaseUser(int pipe, int user)
        {
            if(ClientEngine != null)
                ClientEngine.ReleaseUser(pipe, user);
        }

        public bool BReleaseSteamPipe(int pipe)
        {
            if(ClientEngine != null)
                return ClientEngine.BReleaseSteamPipe(pipe);
            return true;
        }

        internal Steam4NET.IClientApps GetIClientApps(int HSteamUser, int HSteamPipe)
        {
            return ClientEngine.GetIClientApps<Steam4NET.IClientApps>(HSteamUser, HSteamPipe);
        }

        internal Steam4NET.IClientUser GetIClientUser(int HSteamUser, int HSteamPipe)
        {
            return ClientEngine.GetIClientUser<Steam4NET.IClientUser>(HSteamUser, HSteamPipe);
        }

        internal Steam4NET.IClientAppManager GetIClientAppManager(int HSteamUser, int HSteamPipe)
        {
            return ClientEngine.GetIClientAppManager<Steam4NET.IClientAppManager>(HSteamUser, HSteamPipe);
        }
    }
}
