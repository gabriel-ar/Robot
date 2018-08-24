using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Robot.SettingsControl {

    [Designer(typeof(SettingsControlDesign))]
    public partial class SettingsControl:SplitContainer {

        public delegate void SelectedIndexChangedDlg();
        public event EventHandler SelectedIndexChanged;

        int selected_index = 1;
        public SettingsItem selected_item = null;

        XList<SettingsItem> items;

        internal TreeView tv;

        public SettingsControl() {
            items = new XList<SettingsItem>();
            items.ItemAdded += ItemAdded;

            tv = new TreeView();
            tv.Dock = DockStyle.Fill;
            tv.Indent = 30;
            tv.FullRowSelect = true;
            tv.ShowRootLines = false;
            tv.HideSelection = false;
            tv.NodeMouseClick += NodeClick;
            tv.Margin = new Padding(3, 10, 3, 0);

            Panel1.Controls.Add(tv);
            }

        private void NodeClick(object sender, TreeNodeMouseClickEventArgs e) {
            ShowItem(e.Node.Index);
            }

        [Category("Data")]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public XList<SettingsItem> Items {
            get { return items; }
            }

        private void ItemAdded(SettingsItem si) {
            si.index = items.Count - 1;
            tv.Nodes.Add("");
            si.TabTextChanged += UpdateTabText;
            }

        void UpdateTabText(int index, string txt) {
            tv.Nodes[index].Text = txt;
            }

        //Solo para el Designer
        public void UpdateList() {
            tv.Nodes.Clear();

            foreach(SettingsItem si in items) {
                tv.Nodes.Add(si.TabText);
                }
            }

        public void ShowItem(int bb_index, bool ext = false) {

            Panel2.Controls.Clear();
            Panel2.Controls.Add(items[bb_index]);

            selected_index = bb_index;
            selected_item = items[bb_index];

            if(ext)
                tv.SelectedNode = tv.Nodes[bb_index];
            }

        }

    }
