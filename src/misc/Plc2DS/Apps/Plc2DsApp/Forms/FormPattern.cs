using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using System.Text.RegularExpressions;

namespace Plc2DsApp
{
	public partial class FormPattern: DevExpress.XtraEditors.XtraForm
	{
        LS.PlcTagInfo[] tags = [];
        LS.PlcTagInfo[] tagsNotYet = [];
        public LS.PlcTagInfo[] TagsChosen = [];
        void updateUI()
        {
            tbNumTagsAll.Text = tags.Length.ToString();
            tbNumTagsChosen.Text = TagsChosen.Length.ToString();
            tbNumTagsNotyet.Text = tagsNotYet.Length.ToString();
        }
        void showTags(LS.PlcTagInfo[] tags) => FormGridTags.ShowTags(tags);

        public FormPattern(LS.PlcTagInfo[] tags, Pattern[] patterns)
		{
            InitializeComponent();

            this.tags = tags;
            tagsNotYet = tags;

            gridControl1.DataSource = patterns;
            gridView1.AddActionColumn<Pattern>("Apply", p =>
            {
                return ("Apply", new Action<Pattern>(p =>
                {
                    var pattern = new Regex(p.PatternString, RegexOptions.Compiled);
                    var gr = tagsNotYet.GroupByToDictionary(t => pattern.IsMatch(t.CsGetName()));

                    if (gr.ContainsKey(true))
                    {
                        var form = new FormGridTags(gr[true], true) { Text = "Confirm selection.." };
                        if (DialogResult.OK == form.ShowDialog())
                        {
                            TagsChosen = TagsChosen.Concat(gr[true]).ToArray();
                            tagsNotYet = tagsNotYet.Except(TagsChosen).ToArray();
                            updateUI();
                        }
                    }
                    else
                        MessageBox.Show("No matches found.");
                }));
            });
            btnOK    .Click += (s, e) => { Close(); DialogResult = DialogResult.OK; };
            btnCancel.Click += (s, e) => { Close(); DialogResult = DialogResult.Cancel; };
        }

        private void FormPattern_Load(object sender, EventArgs e)
        {
            btnShowAllTags.Click += (s, e) => showTags(tags);
            btnShowNotyetTags.Click += (s, e) => showTags(tagsNotYet);
            btnShowChosenTags.Click += (s, e) => showTags(TagsChosen);
            updateUI();
        }
    }

    public class Pattern
    {
        public string Name { get; set; }
        public string PatternString { get; set; }
        public string Description { get; set; }
    }
}