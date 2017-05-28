using CarbyneSteamContext;
using IotWeb_CarbyneFork.Common.Http;
using IotWeb_CarbyneFork.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
        //static bool ServerHold = true;
        static object LockConsole = new object();

        private static NotifyIcon Tray = null;
        //private static bool poison = false;

        static HttpServer server = null;
        //static WebSocketHandler OnEventRaisedSocketHandler = null;
        static HttpHandler FunctionHttpHandler = null;

        static void Main(string[] args)
        {
            int port = 0;
            if (args.Length > 0)
            {
                int.TryParse(args[0], out port);
            }

            if(port <= 0)
            {
                MessageBox.Show("Please supply a port paramater that is within the valid range.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string namedPipeCustom = string.Empty;
            //if (args.Length > 1)
            //{
            //    namedPipeCustom = args[1].Trim();
            //}

            //if(port == 0)
            {
                MenuItem mExit = new MenuItem("Exit", new EventHandler(Exit));
                MenuItem mPort = new MenuItem($"Port: {port}");
                mPort.Enabled = false;
                ContextMenu Menu = new ContextMenu(new MenuItem[] { mExit, mPort });

                Tray = new NotifyIcon()
                {
                    Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                    Visible = true,
                    Text = "Carbyne Steam Interop Server",
                    ContextMenu = Menu
                };
            }

            SteamContext context = SteamContext.GetInstance();
            context.Init();


            //OnEventRaisedSocketHandler = new WebSocketHandler();
            FunctionHttpHandler = new HttpHandler(context);

            server = new HttpServer(port);
            
            server.AddHttpRequestHandler(
                "/",
                FunctionHttpHandler
            );

            //server.AddWebSocketRequestHandler(
            //    "/OnEventRaised/",
            //    OnEventRaisedSocketHandler
            //);

            server.Start();

            Application.Run();

            /*while (!poison)
            {
                Thread.Sleep(1000);
            }*/

            server.Stop();
            if (context != null)
            {
                context.Shutdown();
            }

            if (Tray != null)
            {
                Tray.Dispose();
            }
        }

        private static void Exit(object sender, EventArgs e)
        {
            Tray.Dispose();
            //poison = true;
            Application.Exit();
        }
    }

    /*class WebSocketHandler : IWebSocketRequestHandler
    {
        HashSet<WebSocket> Sockets = new HashSet<WebSocket>();

        public bool WillAcceptRequest(string uri, string protocol)
        {
            Console.WriteLine($"{uri}\t{protocol}");
            //return (uri.Length == 0) && (protocol == "echo");
            return true;
        }

        public void Connected(WebSocket socket)
        {
            Sockets.Add(socket);
            socket.ConnectionClosed += Socket_ConnectionClosed;
            //socket.DataReceived += OnDataReceived;
        }

        private void Socket_ConnectionClosed(WebSocket socket)
        {
            Sockets.Remove(socket);
        }

        public void SendToAll(string message)
        {
            foreach (WebSocket socket in Sockets)
            {
                socket.Send(message);
            }
        }

        //void OnDataReceived(WebSocket socket, string frame)
        //{
        //    Console.WriteLine($"{frame}");
        //    socket.Send(frame);
        //}
    }*/

    class HttpHandler : IHttpRequestHandler
    {
        // Instance variables
        private string m_defaultFile;
        private SteamContext context;

        public HttpHandler(SteamContext context, string defaultFile = null)
        {
            m_defaultFile = defaultFile;
            this.context = context;
        }

        public void HandleRequest(string uri, HttpRequest request, HttpResponse response, HttpContext httpcontext)
        {
            string[] uriParts = uri.Split('/');
            System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(request.QueryString);

            if (uriParts.Length <= 1)
            {
                if (request.Method != HttpMethod.Get)
                    throw new HttpMethodNotAllowedException();

                //response.Headers[HttpHeaders.ContentType] = @"text/html; charset=UTF-8";
                //response.ResponseCode = HttpResponseCode.Ok;

                //byte[] raw = Encoding.UTF8.GetBytes(Resources.index);
                //response.Content.Write(raw, 0, raw.Length);

                throw new HttpNotFoundException();

                return;
            }
            
            switch (uriParts[0])
            {
                case "SteamContext":
                    if (uriParts.Length > 1)
                    {
                        switch (uriParts[1])
                        {
                            case "IsInstalled":
                                SendReponseData(request, response, context.IsInstalled(UInt64.Parse(query["GameID"])));
                                return;
                            case "GetGameLibraries":
                                SendReponseData(request, response, context.GetGameLibraries());
                                return;
                            case "InstallGame":
                                {
                                    Steam4NET.EAppUpdateError? rawReturn = context.InstallGame(UInt64.Parse(query["GameID"]), int.Parse(query["GameLibraryIndex"]));
                                    SendReponseData(request, response, rawReturn.HasValue ? (int?)rawReturn.Value : null);
                                }
                                return;
                            case "GetOwnedApps":
                                SendReponseData(request, response, context.GetOwnedApps());
                                return;
                            case "GetGoldSrcMods":
                                SendReponseData(request, response, context.GetGoldSrcMods());
                                return;
                            case "GetSourceMods":
                                SendReponseData(request, response, context.GetSourceMods());
                                return;
                            default:
                                throw new HttpNotFoundException();
                        }
                    }
                    else
                    {
                        // readme?
                    }
                    break;
            }

            throw new HttpNotFoundException();
        }

        private void SendReponseData<T>(HttpRequest request, HttpResponse response, T value)
        {
            if (request.Method != HttpMethod.Get)
                throw new HttpMethodNotAllowedException();

            string output = JsonConvert.SerializeObject(value);

            response.Headers[HttpHeaders.ContentType] = @"text; charset=UTF-8";
            response.ResponseCode = HttpResponseCode.Ok;

            TextWriter writer = new StreamWriter(response.Content);
            writer.Write(output);
            writer.Flush();

            return;
        }
    }
}
