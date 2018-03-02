#define USE_ARGS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

using Xilium.CefGlue;

namespace SharedPluginServer
{

    //Main application


    static class Program
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static int defWidth = 1280;
        static int defHeight = 720;
        static string defUrl = "http://test.webrtc.org";
        static string defFileName = "MainSharedMem";
        static readonly string temporaryDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        static string defInFileName = "InSharedMem";
        static string defOutFileName = "OutSharedMem";

        static bool useWebRTC = false;

        static bool EnableGPU = false;

        [STAThread]
        static int Main(string[] args) {
            Log.Info("parsing command args");
            if (!ParseCommandLine(args)) {
                Log.Error("command line parse error");
                return 0;
            }
            Log.InfoFormat("Starting plugin, settings:width:{0},height:{1},url:{2},memfile:{3},inMem:{4},outMem:{5}, WebRtc:{6},Enable GPU:{7}",
                defWidth, defHeight, defUrl, defFileName, defInFileName, defOutFileName, useWebRTC, EnableGPU);

            Log.InfoFormat("Statring cef runtime");

            if (!CefRintimePrepare(args, temporaryDirectoryPath,useWebRTC,EnableGPU)) {
                Log.Error("cef runtime initialisation failed");
                return 0; //immediate exit
            }


            try {
                Log.Info("starting cef worker");
                CefWorker worker = new CefWorker();
                    worker.Init(defWidth, defHeight, defUrl);
                    Log.Info("Binding shared memory");
                    SharedTextureWriter server = new SharedTextureWriter(defFileName, defWidth * defHeight * 4);
                    MessageReader inSrv = MessageReader.Create(defInFileName, 10000);
                    MessageWriter outSrv = MessageWriter.Create(defOutFileName, 10000);
                    Log.Info("complete to bind shared memory, ready and wait");
                    var app = new App(worker, server, inSrv, outSrv, false);
                    Log.Info("Enter main loop");
                    try {
                        while (app.IsRunning) {
                            Application.DoEvents();
                            app.CheckMessage(); //check incoming messages
                        }
                    }
                    catch (Exception e) {
                        Log.ErrorFormat("abnormal exit main loop{0}", e.Message);
                    }

                    Log.Info("Exit main loop END DISPOSING ALL");
                    worker.Dispose();
                    server.Dispose();
                    inSrv.Dispose();
                    outSrv.Dispose();
            }
            catch (Exception e) {
                Log.ErrorFormat("Unclean exit error {0}", e.Message);
            }
            GC.Collect();
            GC.WaitForFullGCComplete(-1);
            Log.Info("CefRuntime.Shutdown");
            CefRuntime.Shutdown();
            Log.Info("Final exit");
            return 0;
        }

        
        static bool ParseCommandLine(string[] args) {
            try {
                if (args.Length > 0 && args[0] != "--type=renderer") {
                    if (args.Length > 1) {
                        defWidth = Int32.Parse(args[0]);
                        defHeight = Int32.Parse(args[1]);
                    }

                    if (args.Length > 2)
                        defUrl = args[2];
                    if (args.Length > 3)
                        defFileName = args[3];
                    if (args.Length > 4)
                        defInFileName = args[4];
                    if (args.Length > 5)
                        defOutFileName = args[5];
                    if (args.Length > 6)
                        if (args[6] == "1")
                            useWebRTC = true;
                    if (args.Length > 7)
                        if (args[7] == "1")
                            EnableGPU = true;
                }
            }
            catch (Exception ex) {
                Log.ErrorFormat("{0} error", ex.Message);
            }
            

            return true;
        }
        
        static bool CefRintimePrepare(string[] args,string temporaryDirectoryPath, bool useWebRTC,bool EnableGPU) {
            
            try {
                string path = Directory.GetCurrentDirectory();
                var runtimepath = path;
                var clientpath = Path.Combine(runtimepath, "cefclient.exe");
                var resourcepath = runtimepath;
                var localepath = Path.Combine(resourcepath, "locales");
                Log.Info("===============START================");
                CefRuntime.Load(runtimepath); //using native render helper
                Log.Info("appending disable cache keys");
                CefMainArgs cefMainArgs = new CefMainArgs(args) {};
                var cefApp = new WorkerCefApp(useWebRTC, EnableGPU);
                int exit_code = CefRuntime.ExecuteProcess(cefMainArgs, cefApp, IntPtr.Zero);

                if (exit_code >= 0) {
                    Log.ErrorFormat("CefRuntime return " + exit_code);
                    return false;
                }
                var cefSettings = new CefSettings
                {
                    SingleProcess = false,
                    MultiThreadedMessageLoop = true,
                    WindowlessRenderingEnabled = true,
                    //
                    BrowserSubprocessPath = clientpath,
                    FrameworkDirPath = runtimepath,
                    ResourcesDirPath = resourcepath,
                    LocalesDirPath = localepath,
                    LogFile = Path.Combine(Path.GetTempPath(), new Guid().ToString() + ".log"),
                    Locale = "en-US",
                    LogSeverity = CefLogSeverity.Error,
                    //RemoteDebuggingPort = 8088,
                    NoSandbox = true,
                    //CachePath = temporaryDirectoryPath

                };
                CefRuntime.Initialize(cefMainArgs, cefSettings, cefApp, IntPtr.Zero);
                /////////////
            }
            catch (Exception ex) {
                Log.Info("EXCEPTION ON CEF INITIALIZATION:" + ex.Message + "\n" + ex.StackTrace);
                return false;
            }
            return true;
        }
    }
}
