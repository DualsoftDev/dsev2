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
            Application.Run(new FormMain());
        }
    }
}
