using DevExpress.XtraEditors;
using log4net.Config;

namespace Plc2DsApp {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            //var regex = new Regex("^(?<station>S\\d+)(?<device>[a-zA-Z]+\\d+)_.*$");
            //var match = regex.Match("S302RBT4_A_B");

            var regex = new Regex(@"(?<=_)ROBOT\d*|RB\d*(?=_)");
            var match = regex.Match("S302_ROBOT12_A_B");


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


            var root = ((log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository()).Root;
            var form = new FormMain().Tee(f => f.PlaceAtScreenCenter());
            root.AddAppender(form);

            Noop();

            form.Load += (s, e) =>
            {
                logger.Info($":: ===== Starting up application..");
                logger.Info($":: Base directory = {baseDir}");
            };

            Application.Run(form);
        }
    }
}
