using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Robot.SettingsControl {
    
    public partial class SettingsItem:Panel {

        string tab_text;
        public int index;
        
        public delegate void TabTextChangedDlg(int index, string new_txt);
        public event TabTextChangedDlg TabTextChanged;

        public SettingsItem() {
            InitializeComponent();
            Dock = DockStyle.Fill;

            tab_text = "";
            index = 0;
            }

        
        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string TabText {
            get {
                return tab_text;
                }
            set {
                tab_text = value;
                if(TabTextChanged != null)
                    TabTextChanged.Invoke(index, tab_text);
                }
            }

        }
    }
