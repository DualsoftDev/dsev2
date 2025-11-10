using Microsoft.Web.WebView2.WinForms;
using PLC.Convert.FS;
using PLC.Convert.LSCore;
using PLC.Convert.LSCore.Expression;
using PLC.Convert.MX;
using PLC.Convert.Rockwell;
using PLC.Convert.Siemens;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ClassTagGenerator;
using static PLC.Convert.FS.ConvertCoilModule;

namespace PLC.Convert.Mermaid
{
    public partial class FormMermaid : Form
    {
     

        private void InitializeEvent()
        {

            button_openRockwell.Click += async (s, e) => await OpenPLCRockwellABAsync();
            button_openDir.Click += async (s, e) => await OpenPLCDirAsync();
            button_MelsecConvert.Click += async (s, e) => await OpenPLCMelsecAsync();
            button_SiemensConvert.Click += async (s, e) => await OpenPLCSiemensAsync();
            button_LSEConvert.Click += async (s, e) => await OpenPLCLSEAsync();
            
        }

        /// **PLC 프로그램을 불러와 Mermaid 변환**
        private async Task<Tuple<string, string>> ImportProgramXG5K(string file, bool bExportEdges, bool bUsingComment)
        {

            this.Text = "XG5000 load: " + file;

            Tuple<List<Rung>, ProgramInfo> result = await ImportXG5kPath.LoadAsync(file);
            List<Rung> rungs = result.Item1;
            ProgramInfo infos = result.Item2;

            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"LLM/{Path.GetFileNameWithoutExtension(file)}.mmd");

            // **Rung 데이터를 Dictionary<string, List<string>> 형태로 변환**
            var coils = rungs
                .SelectMany(rung => rung.RungExprs.OfType<Terminal>())
                .Where(terminal => terminal.HasInnerExpr);

            if (bExportEdges)
            {
                var mermaidEdges  = MermaidExportModule.ConvertEdges(coils, bUsingComment);
                File.WriteAllText(path.Replace(".mmd", ".mermaid"), mermaidEdges, Encoding.UTF8);
            }

            // **Mermaid 변환 실행**
            var mermaidText = MermaidExportModule.Convert(coils, bUsingComment);

            return Tuple.Create(mermaidText, path);
        }

        private void OpenInitSetting()
        {
            webView.Source = new Uri("https://dualsoft.com");  // 빈 페이지 로드   
        }


        /// **PLC 데이터 불러오기 및 Mermaid 변환 실행**
        private async Task OpenPLCDirAsync()
        {
            try
            {
                OpenInitSetting();
                var files = FileOpenSave.OpenDirFiles();
                if (files == null || files.Count == 0) return;
                foreach (var file in files)
                    await exportMermaid(file, true, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일을 불러오는 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private async Task OpenPLCRockwellABAsync()
        {
            try
            {
                OpenInitSetting();
                var files = FileOpenSave.OpenFiles(false, "l5k");
                if (files == null || files.Length == 0) return;

                string file = files.First();
                var networks = ConvertRockwellModule.parseABFile(file);
                IEnumerable<Terminal> coils = getCoils(networks);

                var mermaidText = MermaidExportModule.ConvertEdges(coils, false);
                File.WriteAllText(file.ToLower().Replace($".l5k", ".mermaid"), mermaidText, Encoding.UTF8);
                LoadMermaidGraph(mermaidText);

                await Task.Delay(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일을 불러오는 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        //private async Task OpenPLCAsync() // LS Electric abnormal contact 사용
        //{
        //    try
        //    {
        //        OpenInitSetting();
        //        var files = FileOpenSave.OpenFiles();
        //        if (files == null || files.Length == 0) return;

        //        string file = files.First();
        //        await exportMermaid(file);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("파일을 불러오는 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}
        private async Task OpenPLCMelsecAsync()
        {
            try
            {
                OpenInitSetting();
                var files = FileOpenSave.OpenFiles(true, "csv");
                if (files == null || files.Length == 0) return;
                var dir = Path.GetDirectoryName(files.First());
                
                Network[] networks = ConvertMitsubishiModule.parseMXFile(files); 
                IEnumerable<Terminal> coils = getCoils(networks);

                var mermaidText = MermaidExportModule.ConvertEdges(coils, false);
                File.WriteAllText(dir+".mermaid", mermaidText, Encoding.UTF8);
                LoadMermaidGraph(mermaidText);


                //var file = await ConvertXGI(files);
                //ConvertLSE(file);           
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일을 불러오는 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private async Task OpenPLCLSEAsync()
        {
            try
            {
                OpenInitSetting();
                var files = FileOpenSave.OpenFiles(false, "xml");
                if (files == null || files.Length == 0) return;
                var file = files.First();
                await Task.Delay(0);

                Network[] networks = ConvertLSEModule.parseLSEFile(file);
                IEnumerable<Terminal> coils = getCoils(networks);

                var mermaidText = MermaidExportModule.ConvertEdges(coils, false);
                File.WriteAllText(file.Replace($".xml", ".mermaid"), mermaidText, Encoding.UTF8);
                LoadMermaidGraph(mermaidText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일을 불러오는 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OpenPLCSiemensAsync()
        {
            await Task.Delay(0);
            try
            {
                OpenInitSetting();
                var extenstions = "AWL";
                var files = FileOpenSave.OpenFiles(false, extenstions);
                if (files == null || files.Length == 0) return;
                var file = files.First();

                Network[] networks = ConvertSiemensModule.parseSiemensFile(file);
                IEnumerable<Terminal> coils = getCoils(networks);

                var mermaidText = MermaidExportModule.ConvertEdges(coils, false);
                File.WriteAllText(file.Replace($".{extenstions}", ".mermaid"), mermaidText, Encoding.UTF8);
                LoadMermaidGraph(mermaidText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일을 불러오는 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

         

        }

        private async Task exportMermaid(string file, bool exportEdges = false, bool usingComment = false)
        {
            var (mermaidText, path) = await ImportProgramXG5K(file, exportEdges, usingComment);
            // ✅ **Mermaid 파일 저장 (.mmd)**
            File.WriteAllText(path, mermaidText, Encoding.UTF8);

            LoadMermaidGraph(mermaidText);
        }
    }
}
