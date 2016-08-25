﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageLibrary;
using SharedPluginServer.Interprocess;

namespace SharedPluginServer
{

    //just a glue
    public class App
    {
        private static readonly log4net.ILog log =
 log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        private SharedMemServer _memServer;

        private SocketServer _controlServer;

        private CefWorker _mainWorker;

        public App(CefWorker worker, SharedMemServer memServer, SocketServer commServer)
        {
            _memServer = memServer;
            _mainWorker = worker;
            _controlServer = commServer;

            _mainWorker.SetMemServer(_memServer);

            SocketServer.OnReceivedMessage += HandleMouse;
        }

        public void HandleMouse(EventPacket msg)
        {
            log.Info("________Packet:" + msg.Type);

            switch (msg.Type)
            {
                case EventType.Generic:
                {
                    GenericEvent genericEvent=msg.Event as GenericEvent;
                    if (genericEvent != null)
                    {
                        switch (genericEvent.Type)
                        {
                             case GenericEventType.Shutdown:
                            {
                                try
                                {
                                    log.Info("==============SHUTTING DOWN==========");
                                    SocketServer.OnReceivedMessage -= HandleMouse;
                                        _mainWorker.Shutdown();
                                    log.Info("___MAIN");
                                     _memServer.Dispose();
                                   log.Info("___MEM");
                                    _controlServer.Shutdown();
                                     log.Info("___CONTROL");
                                    Application.Exit();
                                  // Environment.Exit(Environment.ExitCode);
                                }
                                catch (Exception e)
                                {

                                    log.Info("______EXIT:"+e.StackTrace);
                                }

                                break;
                            }
                        }
                    }
                    break;
                }

                case EventType.Keyboard:
                {
                        KeyboardEvent keyboardEvent=msg.Event as KeyboardEvent;
                    if (keyboardEvent != null)
                    {
                     
                                    _mainWorker.CharEvent(keyboardEvent.Key,keyboardEvent.Type);
                         
                    }
                    break;
                }
                case EventType.Mouse:
                    {
                        MouseMessage mouseMessage=msg.Event as MouseMessage;
                        if (mouseMessage != null)
                        {
                            switch (mouseMessage.Type)
                            {
                                case MouseEventType.LButtonDown:
                                    _mainWorker.MouseEvent(mouseMessage.X, mouseMessage.Y, false);
                                    break;
                                case MouseEventType.LButtonUp:
                                    _mainWorker.MouseEvent(mouseMessage.X, mouseMessage.Y, true);
                                    break;
                                case MouseEventType.Move:
                                    _mainWorker.MouseMoveEvent(mouseMessage.X, mouseMessage.Y);
                                    break;
                            }
                        }

                        break;
                    }
            }

           
        }

        ~App()
        {
            //_mainWorker.Shutdown();
        }
    }


    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            //log4net.Config.XmlConfigurator.Configure();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            CefWorker worker=new CefWorker();
           worker.Init();
            SharedMemServer _server=new SharedMemServer();
            _server.Init(1);

           // NamedPipeServer.PipeName = "MainCommChannel";
           // ThreadStart pipeStart=new ThreadStart(NamedPipeServer.CreatePipeServer);

           /* Thread listenerThread=new Thread(pipeStart);
            listenerThread.SetApartmentState(ApartmentState.STA);
            listenerThread.IsBackground = true;
            listenerThread.Start();*/
            SocketServer ssrv=new SocketServer();
            ssrv.Init();

            var app=new App(worker,_server,ssrv);
           // var app = new App(null, _server, ssrv);

            Application.Run();

           // Application.Run(new Form1(worker,_server,ssrv));

            //ssrv.Shutdown();
        }
    }
}
