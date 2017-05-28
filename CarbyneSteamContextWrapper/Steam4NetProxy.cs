using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steam4NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarbyneSteamContext;
using System.Net.NetworkInformation;
using System.Net;
using System.Collections.Specialized;

namespace CarbyneSteamContextWrapper
{
    class Steam4NETProxy : IDisposable
    {
        private Process server;

        private Dictionary<Guid, object> InstanceCache = new Dictionary<Guid, object>();
        private WebClient client = new WebClient();
        private int ProxyPort;


        public Steam4NETProxy()
        {

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

        ~Steam4NETProxy()
        {
            Dispose(false);
        }
        #endregion Dispose

        private int GetFreePortInRange(int PortStartIndex, int PortEndIndex)
        {
            //DevUtils.LogDebugMessage(string.Format("GetFreePortInRange, PortStartIndex: {0} PortEndIndex: {1}", PortStartIndex, PortEndIndex));
            try
            {
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

                IPEndPoint[] tcpEndPoints = ipGlobalProperties.GetActiveTcpListeners();
                List<int> usedServerTCpPorts = tcpEndPoints.Select(p => p.Port).ToList<int>();

                IPEndPoint[] udpEndPoints = ipGlobalProperties.GetActiveUdpListeners();
                List<int> usedServerUdpPorts = udpEndPoints.Select(p => p.Port).ToList<int>();

                TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
                List<int> usedPorts = tcpConnInfoArray.Where(p => p.State != TcpState.Closed).Select(p => p.LocalEndPoint.Port).ToList<int>();

                usedPorts.AddRange(usedServerTCpPorts.ToArray());
                usedPorts.AddRange(usedServerUdpPorts.ToArray());

                int unusedPort = 0;

                for (int port = PortStartIndex; port < PortEndIndex; port++)
                {
                    if (!usedPorts.Contains(port))
                    {
                        unusedPort = port;
                        break;
                    }
                }
                //DevUtils.LogDebugMessage(string.Format("Local unused Port:{0}", unusedPort.ToString()));

                if (unusedPort == 0)
                {
                    //DevUtils.LogErrorMessage("Out of ports");
                    throw new ApplicationException("GetFreePortInRange, Out of ports");
                }

                return unusedPort;
            }
            catch (Exception ex)
            {

                string errorMessage = ex.Message;
                //DevUtils.LogErrorMessage(errorMessage);
                throw;
            }
        }

        private int GetLocalFreePort()
        {
            int hemoStartLocalPort = 49152;
            int hemoEndLocalPort = 65535;
            int localPort = GetFreePortInRange(hemoStartLocalPort, hemoEndLocalPort);
            //DevUtils.LogDebugMessage(string.Format("Local Free Port:{0}", localPort.ToString()));
            return localPort;
        }


        public T SendFunctionCall<T>(string function, NameValueCollection paramaters = null)
        {
            string download = client.DownloadString($"http://localhost:{ProxyPort}/" + function + paramaters?.ToQueryString());

            return JsonConvert.DeserializeObject<T>(download);
        }

        /*internal void Ping()
        {
            InteropFunctionReturn retVal = SendFunctionCall(new InteropFunctionCall()
            {
                Command = "Ping"
            }, false);
            //return retVal.Return.ToObject<bool>();
        }*/

        public bool IsInstalled(UInt64 GameID)
        {
            return SendFunctionCall<bool>("SteamContext/IsInstalled",
                new NameValueCollection()
                {
                    { "GameID", GameID.ToString() }
                });
        }

        public void Shutdown()
        {
            if (server != null && server.IsRunning()) // make sure it's our service to stop
            {
                server.Close();
            }
        }

        public EAppUpdateError? InstallGame(ulong gameID, int gameLibraryIndex)
        {
            return SendFunctionCall<EAppUpdateError?>("SteamContext/InstallGame",
                new NameValueCollection()
                {
                    { "GameID", gameID.ToString() },
                    { "GameLibraryIndex", gameLibraryIndex.ToString() }
                });
        }

        public List<SteamLaunchableApp> GetOwnedApps()
        {
            return SendFunctionCall<List<SteamLaunchableApp>>("SteamContext/GetOwnedApps");
        }

        public List<SteamLaunchableModGoldSrc> GetGoldSrcMods()
        {
            return SendFunctionCall<List<SteamLaunchableModGoldSrc>>("SteamContext/GetGoldSrcMods");
        }

        public List<SteamLaunchableModSource> GetSourceMods()
        {
            return SendFunctionCall<List<SteamLaunchableModSource>>("SteamContext/GetSourceMods");
        }

        public string[] GetGameLibraries()
        {
            return SendFunctionCall<string[]>("SteamContext/GetGameLibraries");
        }

        public bool Init(string ProxyServerPath = null, bool SearchSubfolders = false)
        {
            ProxyPort = GetLocalFreePort();

            // we have no way to make sure the thing isn't running globally to stick our random suffix on it, and what would the point be, useless feature
            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = "CarbyneSteamContextServer.exe",
                Arguments = ProxyPort.ToString(),
                UseShellExecute = false
            };
            if (!string.IsNullOrWhiteSpace(ProxyServerPath))
            {
                string[] possibleServers = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), ProxyServerPath), "CarbyneSteamContextServer.exe", SearchSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                if (possibleServers.Length > 0)
                {
                    info.FileName = possibleServers[0];
                }
            }
            server = Process.Start(info);

            return false;
        }
    }
}
