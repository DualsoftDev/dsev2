using DevExpress.XtraEditors;
using log4net.Config;

namespace Plc2DsApp {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // 전역 예외 핸들러 설치
            UnhandledExceptionHandler.InstallUnhandledExceptionHandler();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);


            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!File.Exists(Path.Combine(baseDir, "log4net.config")))
                MessageBox.Show($"log4net.config not found");

            XmlConfigurator.Configure(new FileInfo(Path.Combine(baseDir, "log4net.config")));
            var logger = LogManager.GetLogger("AppLogger");
            DcLogger.Logger = logger;
            logger.Info($":: ===== Starting up addin..");

            logger.Info($":: Base directory = {baseDir}");


            var root = ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root;
            var form = new FormMain();
            root.AddAppender(form);
            Application.Run(form);
        }
    }
}
