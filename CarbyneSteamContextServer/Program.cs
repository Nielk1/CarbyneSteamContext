using CarbyneSteamContext;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CarbyneSteamContextServer
{
    class Program
    {
        static bool ServerHold = true;
        static object LockConsole = new object();

        private static NotifyIcon Tray = null;

        static void Main(string[] args)
        {
            int timeout = 0;
            if (args.Length > 0)
            {
                int.TryParse(args[0], out timeout);
            }

            string namedPipeCustom = string.Empty;
            //if (args.Length > 1)
            //{
            //    namedPipeCustom = args[1].Trim();
            //}

            if(timeout == 0)
            {
                MenuItem mExit = new MenuItem("Exit", new EventHandler(Exit));
                ContextMenu Menu = new ContextMenu(new MenuItem[] { mExit });

                Tray = new NotifyIcon()
                {
                    Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                    Visible = true,
                    Text = "Carbyne Steam Interop Server",
                    ContextMenu = Menu
                };
            }

            /*// get application GUID as defined in AssemblyInfo.cs
            string appGuid =
                ((GuidAttribute)Assembly.GetExecutingAssembly().
                    GetCustomAttributes(typeof(GuidAttribute), false).
                        GetValue(0)).Value.ToString();

            // unique id for global mutex - Global prefix means it is global to the machine
            string mutexId = string.Format("Global\\{{{0}}}", appGuid);

            // Need a place to store a return value in Mutex() constructor call
            bool createdNew;

            // edited by Jeremy Wiebe to add example of setting up security for multi-user usage
            // edited by 'Marc' to work also on localized systems (don't use just "Everyone") 
            var allowEveryoneRule =
                new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    MutexRights.FullControl, AccessControlType.Allow);
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);

            // edited by MasonGZhwiti to prevent race condition on security settings via VanNguyen
            using (var mutex = new Mutex(false, mutexId, out createdNew, securitySettings))
            {
                // edited by acidzombie24
                var hasHandle = false;
                try
                {
                    try
                    {
                        // note, you may want to time out here instead of waiting forever
                        // edited by acidzombie24
                        // mutex.WaitOne(Timeout.Infinite, false);
                        hasHandle = mutex.WaitOne(100, false);
                        if (hasHandle == false)
                            //throw new TimeoutException("Timeout waiting for exclusive access");
                            return;
                    }
                    catch (AbandonedMutexException)
                    {
                        // Log the fact that the mutex was abandoned in another process,
                        // it will still get acquired
                        hasHandle = true;
                    }*/

            {
                SteamContext context = SteamContext.GetInstance();
                context.Init();
                /*NamedPipeServerStream CallbackCallServer;
                Task.Factory.StartNew(() =>
                {
                    CallbackCallServer = new NamedPipeServerStream($"CarbyneSteam4NET_Callback{namedPipeCustom}", PipeDirection.Out, NamedPipeServerStream.MaxAllowedServerInstances);
                    CallbackCallServer.WaitForConnection();
                    StreamWriter CallbackCallWriter = new StreamWriter(CallbackCallServer);
                    while (ServerHold)
                    {
                        CallbackCallWriter.WriteLine(String.Join("", line.Reverse()));
                        CallbackCallWriter.Flush();
                    }
                });*/

                NamedPipeServerStream DirectCallServer = null;
                Task.Factory.StartNew(() =>
                {
                    lock (LockConsole)
                    {
                        Console.WriteLine("Starting Pipe Server");
                    }
                    DirectCallServer = new NamedPipeServerStream($"CarbyneSteam4NET_Direct{namedPipeCustom}", PipeDirection.InOut, 1);
                    StreamReader DirectCallReader = new StreamReader(DirectCallServer);
                    StreamWriter DirectCallWriter = new StreamWriter(DirectCallServer);
                    lock (LockConsole)
                    {
                        Console.WriteLine("Streams Connected");
                    }
                    DirectCallServer.WaitForConnection();
                    lock (LockConsole)
                    {
                        Console.WriteLine("Client Connected");
                    }

                    while (ServerHold)
                    {
                        Task<string> IncomingTask = DirectCallReader.ReadLineAsync();

                        while (!IncomingTask.IsCompleted && !IncomingTask.IsCanceled && !IncomingTask.IsFaulted)
                        {
                            Thread.Sleep(100);
                        }

                        string Incoming = IncomingTask.Result;

                        if (Incoming == null)
                        {
                            DirectCallServer.Disconnect();
                            DirectCallServer.Dispose();
                            Console.WriteLine("Rebuilding Pipe");
                            DirectCallServer = new NamedPipeServerStream($"CarbyneSteam4NET_Direct{namedPipeCustom}", PipeDirection.InOut, 1);
                            DirectCallReader = new StreamReader(DirectCallServer);
                            DirectCallWriter = new StreamWriter(DirectCallServer);
                            Console.WriteLine("Rebuilt Pipe");

                            //DirectCallServer.WaitForConnection();
                            Task waitingForConnection = DirectCallServer.WaitForConnectionAsync();
                            int TimeoutCounter = 0;
                            while (!waitingForConnection.IsCompleted && !waitingForConnection.IsCanceled && !waitingForConnection.IsFaulted)
                            {
                                Thread.Sleep(100);
                                if (timeout > 0)
                                {
                                    TimeoutCounter += 10;
                                    if (TimeoutCounter >= timeout)
                                    {
                                        break;
                                        //waitingForConnection.
                                    }
                                }
                            }

                            if (timeout > 0 && TimeoutCounter >= timeout)
                            {
                                // we timed out, so we need to GTFO
                                ServerHold = false;
                                break;
                            }
                            else
                            {
                                lock (LockConsole)
                                {
                                    Console.WriteLine("Client Connected");
                                }
                            }
                            continue;
                        }
                        else if (string.IsNullOrWhiteSpace(Incoming))
                        {
                            Console.WriteLine("EmptyData");
                            Thread.Sleep(100);
                        }
                        else
                        {
                            lock (LockConsole)
                            {
                                Console.WriteLine($">{Incoming}");
                            }

                            try
                            {
                                InteropFunctionCall call = JsonConvert.DeserializeObject<InteropFunctionCall>(Incoming);
                                if (call.Command == "Die")
                                {
                                    ServerHold = false;
                                    break;
                                }

                                if (!string.IsNullOrWhiteSpace(call.Function))
                                {
                                    try
                                    {
                                        switch (call.Function)
                                        {
                                            case "IsInstalled":
                                                {
                                                    string OutGoing = JsonConvert.SerializeObject(
                                                        new InteropFunctionReturn()
                                                        {
                                                            Return = context.IsInstalled(call.Paramaters["GameID"].ToObject<UInt64>())
                                                        });
                                                    lock (LockConsole)
                                                    {
                                                        Console.WriteLine($"<{OutGoing}");
                                                    }
                                                    DirectCallWriter.WriteLine(OutGoing);
                                                    DirectCallWriter.Flush();
                                                }
                                                break;
                                            case "GetGameLibraries":
                                                {
                                                    string OutGoing = JsonConvert.SerializeObject(
                                                        new InteropFunctionReturn()
                                                        {
                                                            Return = new JArray(context.GetGameLibraries())
                                                        });
                                                    lock (LockConsole)
                                                    {
                                                        Console.WriteLine($"<{OutGoing}");
                                                    }
                                                    DirectCallWriter.WriteLine(OutGoing);
                                                    DirectCallWriter.Flush();
                                                }
                                                break;
                                            case "InstallGame":
                                                {
                                                    Steam4NET.EAppUpdateError? rawReturn = context.InstallGame(call.Paramaters["GameID"].ToObject<UInt64>(), call.Paramaters["GameLibraryIndex"].ToObject<int>());
                                                    string OutGoing = JsonConvert.SerializeObject(
                                                        new InteropFunctionReturn()
                                                        {
                                                            Return = rawReturn.HasValue ? (int?)rawReturn.Value : null
                                                        });
                                                    lock (LockConsole)
                                                    {
                                                        Console.WriteLine($"<{OutGoing}");
                                                    }
                                                    DirectCallWriter.WriteLine(OutGoing);
                                                    DirectCallWriter.Flush();
                                                }
                                                break;
                                            case "GetOwnedApps":
                                                {
                                                    List<SteamLaunchableApp> rawReturn = context.GetOwnedApps();
                                                    string OutGoing = JsonConvert.SerializeObject(
                                                        new InteropFunctionReturn()
                                                        {
                                                            Return = JArray.FromObject(rawReturn)
                                                        });
                                                    lock (LockConsole)
                                                    {
                                                        Console.WriteLine($"<{OutGoing}");
                                                    }
                                                    DirectCallWriter.WriteLine(OutGoing);
                                                    DirectCallWriter.Flush();
                                                }
                                                break;
                                            case "GetGoldSrcMods":
                                                {
                                                    List<SteamLaunchableModGoldSrc> rawReturn = context.GetGoldSrcMods();
                                                    string OutGoing = JsonConvert.SerializeObject(
                                                        new InteropFunctionReturn()
                                                        {
                                                            Return = JArray.FromObject(rawReturn)
                                                        });
                                                    lock (LockConsole)
                                                    {
                                                        Console.WriteLine($"<{OutGoing}");
                                                    }
                                                    DirectCallWriter.WriteLine(OutGoing);
                                                    DirectCallWriter.Flush();
                                                }
                                                break;
                                            case "GetSourceMods":
                                                {
                                                    List<SteamLaunchableModSource> rawReturn = context.GetSourceMods();
                                                    string OutGoing = JsonConvert.SerializeObject(
                                                        new InteropFunctionReturn()
                                                        {
                                                            Return = JArray.FromObject(rawReturn)
                                                        });
                                                    lock (LockConsole)
                                                    {
                                                        Console.WriteLine($"<{OutGoing}");
                                                    }
                                                    DirectCallWriter.WriteLine(OutGoing);
                                                    DirectCallWriter.Flush();
                                                }
                                                break;
                                            default:
                                                throw new Exception($"Unknown Function \"{call.Function}\"");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        string OutGoing = JsonConvert.SerializeObject(
                                                        new InteropFunctionReturn()
                                                        {
                                                            Exception = ex
                                                        });
                                        lock (LockConsole)
                                        {
                                            Console.WriteLine(ex.ToString());
                                            //Console.WriteLine($"<{OutGoing}");
                                        }
                                        DirectCallWriter.WriteLine(OutGoing);
                                        DirectCallWriter.Flush();
                                    }
                                }
                                //var line = DirectCallReader.ReadLine();
                                //DirectCallWriter.WriteLine(String.Join("", line.Reverse()));
                                //DirectCallWriter.Flush();
                            }
                            catch
                            {

                            }
                        }
                    }
                });

                if (timeout == 0)
                {
                    Application.Run();
                }
                else
                {
                    while (ServerHold)
                    {
                        Thread.Sleep(1000);
                    }
                }

                // cleanup
                try
                {
                    //ServerHold = false;
                    NamedPipeClientStream clt = new NamedPipeClientStream(".", $"CarbyneSteam4NET_Direct{namedPipeCustom}");
                    byte[] dieMessage = "{\"Command\":\"Die\"}\r\n".Select(dr => (byte)dr).ToArray();
                    clt.Write(dieMessage, 0, dieMessage.Length);
                    clt.Flush();
                    clt.Connect();
                    clt.Close();

                    //CallbackCallServer.Close();
                    //CallbackCallServer.Dispose();

                    DirectCallServer.Close();
                    DirectCallServer.Dispose();
                }
                catch { }

                if(context != null)
                {
                    context.Shutdown();
                }
            }
            /*
                }
                finally
                {
                    // edited by acidzombie24, added if statement
                    if (hasHandle)
                        mutex.ReleaseMutex();
                }
            }*/

            if(Tray != null)
            {
                Tray.Dispose();
            }
        }

        private static void Exit(object sender, EventArgs e)
        {
            Tray.Dispose();
            Application.Exit();
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
