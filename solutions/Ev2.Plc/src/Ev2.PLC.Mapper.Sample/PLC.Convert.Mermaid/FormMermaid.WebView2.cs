using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PLC.Convert.Mermaid
{
    public partial class FormMermaid : Form
    {
        private WebView2 webView;
        private TreeView treeView;
        private string fullMermaidCode; // 전체 Mermaid 코드 저장

        public FormMermaid()
        {
            InitializeComponent();
            InitializeTreeView();
            InitializeWebView();
            InitializeEvent();

            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30); // 🟢 다크 모드 배경
            this.Text = "Reverse Viewer";
        }

        private void InitializeTreeView()
        {
            treeView = new TreeView
            {
                Dock = DockStyle.Left,
                Width = 200
            };
            this.Controls.Add(treeView);
            treeView.AfterSelect += TreeView_AfterSelect;
            treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
        }
        private void TreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string selectedSubgraph = ExtractSubgraph(fullMermaidCode, e.Node.Text);

            // 🟢 편집 폼(EditForm) 열기
            EditForm editForm = new EditForm(selectedSubgraph);
            if (editForm.ShowDialog() == DialogResult.OK)
            {
                // 🟢 변경된 내용 다시 적용 가능
                string updatedText = editForm.EditedText;
                DrawMermaidGraph(updatedText);
            }
        }


        private void InitializeWebView()
        {
            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(webView);
            webView.CoreWebView2InitializationCompleted += (s, e) =>
            {
                webView.CoreWebView2.NavigateToString("<html><body><h2>Loading...</h2></body></html>");
            };
            webView.EnsureCoreWebView2Async();
        }

        public void LoadMermaidGraph(string mermaidText)
        {
            fullMermaidCode = mermaidText;
            ParseSubgraphs(mermaidText); // 트리뷰 업데이트
            DrawMermaidGraph(mermaidText);
        }

        public void DrawMermaidGraph(string mermaidText)
        {
            string htmlContent = GenerateMermaidHtml(mermaidText);
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.NavigateToString(htmlContent);
            }
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string selectedSubgraph = e.Node.Text;
            if (selectedSubgraph == "전체 보기")
            {
                DrawMermaidGraph(fullMermaidCode);
            }
            else
            {
                string extractedCode = ExtractSubgraph(fullMermaidCode, selectedSubgraph);
                DrawMermaidGraph(extractedCode);
            }
        }

        private void ParseSubgraphs(string mermaidCode)
        {
            treeView.Nodes.Clear();
            treeView.Nodes.Add("전체 보기");

            var matches = Regex.Matches(mermaidCode, @"subgraph\s+([\w_]+)");
            foreach (Match match in matches)
            {
                treeView.Nodes.Add(match.Groups[1].Value);
            }
        }

        private string ExtractSubgraph(string mermaidCode, string subgraphName)
        {
            var match = Regex.Match(mermaidCode, @$"subgraph\s+{subgraphName}([\s\S]*?)end");
            if (match.Success)
            {
                return $"graph TB;\nsubgraph {subgraphName}\n{match.Groups[1].Value}\nend";
            }
            return "graph TB;\n";
        }

        private string GenerateMermaidHtml(string mermaidCode)
        {
            return $@"
    <!DOCTYPE html>
    <html lang='en'>
    <head>
        <meta charset='UTF-8'>
        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        <title>Mermaid Graph</title>
        <script type='module'>
            import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';
            mermaid.initialize({{
                startOnLoad: true,
                theme: 'dark',  // 🟢 다크 테마 적용
                maxTextSize: Infinity
            }});
        </script>
        <style>
            body {{
                font-family: Arial, sans-serif;
                display: flex;
                justify-content: center;
                align-items: center;
                height: 100vh;
                margin: 0;
                background-color: #1e1e1e; /* 🟢 다크 모드 배경 */
                color: #ffffff; /* 🟢 밝은 텍스트 */
            }}
           
        </style>
    </head>
    <body>
        <div class='mermaid'>
            {mermaidCode}
        </div>
        <script>
            mermaid.run();
        </script>
    </body>
    </html>";
        }



    }
}
