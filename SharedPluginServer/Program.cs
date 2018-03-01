#define USE_ARGS

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using Xilium.CefGlue;

namespace SharedPluginServer
{

    //Main application


    static class Program
    {
        private static readonly log4net.ILog log =
log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);



        /// <summary>
        /// The main entry point for the application.
        /// args:
        /// width,
        /// height,
        /// initialURL,
        /// memory file name,
        /// in memory comm file name,
        /// out memory comm file name,
        /// WebRTC?1:0
        /// Enable GPU? 1:0
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            string path = Directory.GetCurrentDirectory();
            var runtimepath = path;
            var clientpath = Path.Combine(runtimepath, "cefclient.exe");
            var resourcepath = runtimepath;
            var localepath = Path.Combine(resourcepath, "locales");

            log.Info("===============START================");

            try
            {
                CefRuntime.Load(runtimepath); //using native render helper
            }
            catch (DllNotFoundException ex)
            {
                log.ErrorFormat("{0} error", ex.Message);
            }
            catch (CefRuntimeException ex)
            {
                log.ErrorFormat("{0} error", ex.Message);
            }
            catch (Exception ex)
            {
                log.ErrorFormat("{0} error", ex.Message);

            }
            int defWidth = 1280;
            int defHeight = 720;
            string defUrl = "http://test.webrtc.org";
            string defFileName = "MainSharedMem";

            string defInFileName = "InSharedMem";
            string defOutFileName = "OutSharedMem";

            bool useWebRTC = false;

            bool EnableGPU = false;

            if (args.Length>0&&args[0] != "--type=renderer")
            {
               

                if (args.Length > 1)
                {
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
                if (args.Length>6)
                    if (args[6] == "1")
                        useWebRTC = true;
                if (args.Length > 7)
                    if (args[7] == "1")
                        EnableGPU = true;
            }

            log.InfoFormat("Starting plugin, settings:width:{0},height:{1},url:{2},memfile:{3},inMem:{4},outMem:{5}, WebRtc:{6},Enable GPU:{7}",
                defWidth, defHeight, defUrl, defFileName,defInFileName,defOutFileName, useWebRTC,EnableGPU);

            try
            {

             CefMainArgs cefMainArgs=new CefMainArgs(args) {
                 
             };
             var cefApp = new WorkerCefApp(useWebRTC,EnableGPU);

             

             int exit_code = CefRuntime.ExecuteProcess(cefMainArgs, cefApp,IntPtr.Zero);

            if ( exit_code>=0)
            {
                    log.ErrorFormat("CefRuntime return "+exit_code);
                    return exit_code;
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
                LogFile = defFileName+".log",
                Locale = "en-US",
                LogSeverity = CefLogSeverity.Error,
                //RemoteDebuggingPort = 8088,
                NoSandbox = true
            };

            try
            {
                    
              CefRuntime.Initialize(cefMainArgs, cefSettings, cefApp, IntPtr.Zero);
                   
            }
            catch (CefRuntimeException ex)
            {
                log.ErrorFormat("{0} error", ex.Message);

            }
                /////////////
            }
            catch (Exception ex)
            {
                log.Info("EXCEPTION ON CEF INITIALIZATION:"+ex.Message+"\n"+ex.StackTrace);
                throw;
            }



           CefWorker worker = new CefWorker();
           worker.Init(defWidth, defHeight, defUrl);
            log.Info("bind shared memory");
            SharedTextureWriter server = new SharedTextureWriter(defFileName, defWidth * defHeight * 4);
            MessageReader inSrv = MessageReader.Create(defInFileName,10000);
            MessageWriter outSrv = MessageWriter.Create(defOutFileName,10000);
            log.Info("complete to bind shared memory, ready and wait");
            //TODO: the sizes may vary, but 10k should be enough?

            var app = new App(worker, server, inSrv, outSrv, false);
            app.ResetTimer();


           while (app.IsRunning)
            {
                Application.DoEvents();
                //check incoming messages and push outcoming
                app.CheckMessage();
            }
          

            CefRuntime.Shutdown();

            return 0;

        }
    }
}
