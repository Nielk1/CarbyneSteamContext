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

namespace CarbyneSteamContextWrapper
{
    class Steam4NETProxy : IDisposable
    {
        private Process server;
        private NamedPipeClientStream pipe;
        private StreamReader pipeReader;
        private StreamWriter pipeWriter;
        private object pipeLock = new object();

        private Dictionary<Guid, object> InstanceCache = new Dictionary<Guid, object>();

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

        public InteropFunctionReturn SendFunctionCall(InteropFunctionCall call, bool Return = true)
        {
            lock (pipeLock)
            {
                Task WriteLineTask = pipeWriter.WriteLineAsync(JsonConvert.SerializeObject(call));
                int TimeOut = 0;
                while (!WriteLineTask.IsCompleted && !WriteLineTask.IsCanceled && !WriteLineTask.IsFaulted)
                {
                    Thread.Sleep(100);
                    TimeOut += 100;
                    if (TimeOut >= 1000)
                        throw new TimeoutException("The InteropServer did not receive our Data");
                }
                //pipeWriter.Flush();
                if (Return)
                {
                    InteropFunctionReturn retVal = JsonConvert.DeserializeObject<InteropFunctionReturn>(pipeReader.ReadLine());
                    return retVal;
                }
                return null;
            }
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
            InteropFunctionReturn retVal = SendFunctionCall(new InteropFunctionCall()
            {
                Function = "IsInstalled",
                Paramaters = new Dictionary<string, JToken>()
                {
                    { "GameID", GameID }
                }
            });
            return retVal.Return.ToObject<bool>();
        }

        public void Shutdown()
        {
            lock (pipeLock)
            {
                if (server != null && server.IsRunning()) // make sure it's our service to stop
                {
                    // we can't be sure the correct program is running so we might send this to a constant server
                    InteropFunctionReturn retVal = SendFunctionCall(
                    new InteropFunctionCall()
                    {
                        Command = "Die"
                    }, false);
                }
                pipe.Close();
                pipe.Dispose();
            }
        }

        public EAppUpdateError? InstallGame(ulong gameID, int gameLibraryIndex)
        {
            InteropFunctionReturn retVal = SendFunctionCall(new InteropFunctionCall()
            {
                Function = "InstallGame",
                Paramaters = new Dictionary<string, JToken>()
                {
                    { "GameID", gameID },
                    { "GameLibraryIndex", gameLibraryIndex }
                }
            });
            return retVal.Return.ToObject<EAppUpdateError?>();
        }

        public List<SteamLaunchableApp> GetOwnedApps()
        {
            InteropFunctionReturn retVal = SendFunctionCall(new InteropFunctionCall()
            {
                Function = "GetOwnedApps"
            });
            return retVal.Return.ToObject<List<SteamLaunchableApp>>();
        }

        public List<SteamLaunchableModGoldSrc> GetGoldSrcMods()
        {
            InteropFunctionReturn retVal = SendFunctionCall(new InteropFunctionCall()
            {
                Function = "GetGoldSrcMods"
            });
            return retVal.Return.ToObject<List<SteamLaunchableModGoldSrc>>();
        }

        public List<SteamLaunchableModSource> GetSourceMods()
        {
            InteropFunctionReturn retVal = SendFunctionCall(new InteropFunctionCall()
            {
                Function = "GetSourceMods"
            });
            return retVal.Return.ToObject<List<SteamLaunchableModSource>>();
        }

        public string[] GetGameLibraries()
        {
            InteropFunctionReturn retVal = SendFunctionCall(new InteropFunctionCall()
            {
                Function = "GetGameLibraries"
            });
            return retVal.Return.ToObject<string[]>();
        }

        public bool Init(string ProxyServerPath = null, bool SearchSubfolders = false)
        {
            if (Process.GetProcessesByName("CarbyneSteamContextServer").Length == 0)
            {
                // we have no way to make sure the thing isn't running globally to stick our random suffix on it, and what would the point be, useless feature
                ProcessStartInfo info = new ProcessStartInfo()
                {
                    FileName = "CarbyneSteamContextServer.exe",
                    Arguments = "1000",
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
            }
            if (Process.GetProcessesByName("CarbyneSteamContextServer").Length > 0)
            {
                pipe = new NamedPipeClientStream(".", $"CarbyneSteam4NET_Direct", PipeDirection.InOut);
                pipeReader = new StreamReader(pipe);
                pipeWriter = new StreamWriter(pipe);
                try
                {
                    pipe.Connect(1000);
                }
                catch (TimeoutException tex)
                {
                    pipe = null;
                    pipeReader = null;
                    pipeWriter = null;
                    if (server != null) server.Close();
                    return false;
                }
                pipeWriter.AutoFlush = true;
                return true;
            }
            return false;
        }
    }

    public class InteropFunctionCall
    {
        public string Command { get; set; }
        //public string Class { get; set; }
        public string Function { get; set; }
        //public Type Generic { get; set; }
        //public Guid? InstanceID { get; set; }
        public Dictionary<string, JToken> Paramaters { get; set; }
    }

    public class InteropFunctionReturn
    {
        public JToken Return { get; set; }
        public Dictionary<string, JToken> OutParamaters { get; set; }
        public Exception Exception { get; set; }
    }
}
