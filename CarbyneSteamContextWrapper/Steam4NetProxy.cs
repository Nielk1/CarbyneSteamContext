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
            /*string PipeSuffix = string.Empty;
            if (Process.GetProcessesByName("CarbyneSteamContextServer").Length == 0)
            {
                // we have no way to make sure the thing isn't running globally to stick our random suffix on it, and what would the point be, useless feature
                //PipeSuffix = Guid.NewGuid().ToString();
                server = Process.Start(new ProcessStartInfo()
                {
                    FileName = "CarbyneSteamContextServer.exe",
                    //Arguments = $"1000 \"{PipeSuffix}\"",
                    Arguments = $"1000",
                    UseShellExecute = false
                });
            }*/
            if (Process.GetProcessesByName("CarbyneSteamContextServer").Length > 0)
            {
                pipe = new NamedPipeClientStream(".", $"CarbyneSteam4NET_Direct", PipeDirection.InOut);
                pipeReader = new StreamReader(pipe);
                pipeWriter = new StreamWriter(pipe);
                pipe.Connect();
            }
            //pipeReader.ReadLine();
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

        internal bool IsInstalled(UInt64 GameID)
        {
            lock (pipeLock)
            {
                Task WriteLineTask = pipeWriter.WriteLineAsync(JsonConvert.SerializeObject(
                    new InteropFunctionCall()
                    {
                        Function = "IsInstalled",
                        Paramaters = new Dictionary<string, JToken>()
                        {
                            { "GameID", GameID }
                        }
                    }));
                int TimeOut = 0;
                while (!WriteLineTask.IsCompleted && !WriteLineTask.IsCanceled && !WriteLineTask.IsFaulted)
                {
                    Thread.Sleep(100);
                    TimeOut += 100;
                    if (TimeOut >= 1000)
                        return false;
                }
                pipeWriter.Flush();
                InteropFunctionReturn retVal = JsonConvert.DeserializeObject<InteropFunctionReturn>(pipeReader.ReadLine());
                return retVal.Return.ToObject<bool>();
            }
        }

        internal void Shutdown()
        {
            lock (pipeLock)
            {
                if (server != null && server.IsRunning()) // make sure it's our service to stop
                {
                    // we can't be sure the correct program is running so we might send this to a constant server
                    Task WriteLineTask = pipeWriter.WriteLineAsync(JsonConvert.SerializeObject(
                        new InteropFunctionCall()
                        {
                            Command = "Die"
                        }));
                    int TimeOut = 0;
                    while (!WriteLineTask.IsCompleted && !WriteLineTask.IsCanceled && !WriteLineTask.IsFaulted)
                    {
                        Thread.Sleep(100);
                        TimeOut += 100;
                        if (TimeOut >= 1000)
                            break;
                    }
                    pipeWriter.Flush();
                }
                pipe.Close();
                pipe.Dispose();
            }
        }



        internal void Init()
        {
            
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
    }
}
