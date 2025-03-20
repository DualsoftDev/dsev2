using log4net.Core;

using System.Diagnostics;
using System.Drawing;

namespace Plc2DsApp
{
    public partial class FormMain
    {
        public static ILog Logger { get; set; }


        public async void DoAppend(LoggingEvent loggingEvent)
        {
            await this.DoAsync(async tcs =>
            {
                try
                {
                    var msg = loggingEvent.MessageObject.ToString();
                    var level = loggingEvent.Level.Name;
                    var cr = GetLogLevelColor(level).Name;
                    var now = loggingEvent.TimeStamp.ToString("HH:mm:ss.fff");
                    Trace.WriteLine(msg);
                    /*
                     * multi-line message 처리
                     */
                    var lines = msg.SplitByLines().ToArray();
                    if (lines.Length > 0)
                    {
                        var msgLine = lines[0].Replace("{", "{{").Replace("}", "}}");
                        var fmtMsg = string.Format($"<color={cr}>{now} [{level}]: {msgLine}</color>");
                        ucPanelLog1.Items.Add(fmtMsg);

                        for (int i = 1; i < lines.Length; i++)
                        {
                            fmtMsg = $"<color={cr}>    {lines[i]}</color>";
                            ucPanelLog1.Items.Add(fmtMsg);
                        }

                        ucPanelLog1.SelectedIndex = ucPanelLog1.Items.Count - 1;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to append log: {ex}");
                }
                finally { tcs.SetResult(true); }    // 비동기 작업 완료 마킹 (결과 무시)
            });

            Color GetLogLevelColor(string levelName)
            {
                switch (levelName)
                {
                    case "DEBUG": return Color.Orange;
                    case "INFO": return Color.Navy;
                    case "ERROR": return Color.Red;
                    case "WARN": return Color.Brown;
                    default: return Color.Black;
                }
            }

        }
    }
}