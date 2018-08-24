using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Threading;

using System.Runtime.Serialization.Json;


namespace Robot {

    public partial class Main:Form {

        string prefs_file = "settings.json";

        DataContractJsonSerializer json;
        MainData data;

        Controller controller;
        Thread contr_trd;

        System.Threading.Timer data_timer;

        delegate void UpdateUiCb(State status);
        delegate void UpdateDataCb();

        public Main() {

            json = new DataContractJsonSerializer(typeof(MainData));

            if(File.Exists(prefs_file)) {
                StreamReader reader = new StreamReader("settings.json");
                data = (MainData) json.ReadObject(reader.BaseStream);
                data.Initialize();

                reader.Close();
                } else {

                data = new MainData();
                data.SetUp();
                SaveData();
                }

            InitializeComponent();

            InitializeUI();

            //Listeners
            data.StatusChange += UpdateUiSec;
            FormClosing += OnClosing;

            btn_scan.Click += StartScan;

            tb_addr.TextChanged += AddrChanged;
            btn_addr.Click += AddAddr;
            btn_del_addr.Click += DelAddr;

            lb_addr.SelectedIndexChanged += AddrSelected;

            cb_nav.SelectedIndexChanged += Nav;

            tb_add_rgx.TextChanged += CheckRegex;
            btn_add_rgx.Click += AddRegex;
            btn_del_rgx.Click += DelRegex;
            lb_rgx.SelectedIndexChanged += RgxIndexChanged;

            tb_check_rgx.TextChanged += CompRegex;

            btn_browse_sf.Click += BrowseSaveFolder;
            btn_open_fs.Click += OpenFS;

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }

        private void StartScan(object sender, EventArgs e) {

            if(data.status == State.Iddle) {

                contr_trd = new Thread(new ThreadStart(StartController));
                contr_trd.Start();

                btn_scan.Enabled = false;
                btn_scan.Text = "Iniciando";

                } else if(data.status == State.Working) {
                btn_scan.Text = "Pausando";
                btn_scan.Enabled = false;
                controller.Pause();

                } else if(data.status == State.Paused) {
                controller.Restart();
                }
            }

        void StartController() {
            controller = new Controller(ref data);
            controller.Initialize();
            }

        #region UI Live Update
        void UpdateUiSec(State status) {
            if(this.Visible)
                Invoke(new UpdateUiCb(UpdateUi), status);
            }

        void UpdateUi(State status) {

            switch(status) {

                case State.Working:
                    btn_scan.Enabled = true;
                    btn_scan.Text = "Pausar";
                    data_timer = new System.Threading.Timer(new TimerCallback(UpdateDataSec), null, 0, 100);
                    break;

                case State.Paused:
                    data_timer.Dispose();
                    btn_scan.Enabled = true;
                    btn_scan.Text = "Iniciar";
                    lb_con.Text = "Sin Conexiones";
                    break;

                case State.Iddle:
                    data_timer.Dispose();
                    btn_scan.Text = "Iniciar";
                    data.Reset();
                    lb_todo.Text = "-";
                    lb_con.Text = "Finalizado";
                    break;

                }
            }

        void UpdateDataSec(object obj) {
            try {
                Invoke(new UpdateDataCb(UpdateData));
                } catch(Exception) {
                data.Log2("Error al actualizar UI");
                }
            }

        void UpdateData() {
            lb_saved.Text = data.links_saved.Count.ToString();
            lb_todo.Text = data.links_todo.Count.ToString();
            lb_fail.Text = data.links_failed.Count.ToString();


            string actwork = "";
            foreach(WorkerThread wt in data.workers) {
                actwork += "->" + wt.act_work + '\r' + '\n';
                }
            lb_con.Text = actwork;
            }

        #endregion

        #region  UI
        void InitializeUI() {
            bs_main_data.DataSource = data;

            settings_control.ShowItem(0, true);

            btn_del_addr.Enabled = data.org_links.Count > 0;
            btn_scan.Enabled = data.org_links.Count > 0;

            cb_nav.SelectedIndex = (int) data.nav_direction;
            }

        //Address & Nav
        private void AddrChanged(object sender, EventArgs e) {
            string str = tb_addr.Text.Trim();
            btn_addr.Enabled = Regex.IsMatch(str,
                URL.rgx_url, RegexOptions.IgnoreCase);
            }

        private void AddAddr(object sender, EventArgs e) {
            string addr = tb_addr.Text.Trim();
            foreach(string str in data.OrgLinks) {
                if(addr == str) {
                    MessageBox.Show("La dirección ya existe.");
                    return;
                    }
                }

            bs_org_links.Add(addr);

            btn_del_addr.Enabled = true;
            btn_scan.Enabled = true;
            }

        private void DelAddr(object sender, EventArgs e) {

            int indx = lb_addr.SelectedIndex;
            if(indx >= 0) {
                bs_org_links.RemoveAt(indx);

                if(data.OrgLinks.Count == 0) {
                    btn_del_addr.Enabled = false;
                    btn_scan.Enabled = false;
                    }

                } else {
                lb_addr.SelectedIndex = 0;
                }
            }

        private void AddrSelected(object sender, EventArgs e) {
            if(lb_addr.SelectedIndex >= 0)
                tb_addr.Text = (string) lb_addr.Items[lb_addr.SelectedIndex];
            }

        private void Nav(object sender, EventArgs e) {
            data.hnav = !chb_hnav.Checked;
            data.nav_direction = (NavDirection) cb_nav.SelectedIndex;
            }

        //Links

        string rgx_str;
        private void CheckRegex(object sender, EventArgs e) {

            string rgx = tb_add_rgx.Text.Trim();
            if(rgx == "")
                goto deact;

            rgx_str = rgx;

            try {
                Regex new_rule = new Regex(rgx_str, RegexOptions.Singleline);

                tb_add_rgx.ForeColor = Color.Black;
                tb_check_rgx.Enabled = true;
                btn_add_rgx.Enabled = true;

                if(tb_check_rgx.Text.Trim() != "") {
                    if(new_rule.IsMatch(tb_check_rgx.Text.Trim()))
                        lab_test_res.ForeColor = Color.FromArgb(255, 0, 200, 0);
                    else
                        lab_test_res.ForeColor = Color.Red;
                    }

                } catch {

                goto deact;

                }
            return;

            deact:

            tb_add_rgx.ForeColor = Color.Red;
            tb_check_rgx.Enabled = false;
            btn_add_rgx.Enabled = false;
            }

        private void AddRegex(object sender, EventArgs e) {

            foreach(string rgx in data.LinksRegex) {

                if(rgx == rgx_str) {
                    MessageBox.Show("Esta regla ya existe");
                    return;
                    }

                }

            bs_links_rgx.Add(rgx_str);

            btn_del_rgx.Enabled = true;

            }

        private void CompRegex(object sender, EventArgs e) {

            if(rgx_str != null)
                if(Regex.IsMatch(tb_check_rgx.Text.Trim(), rgx_str, RegexOptions.Singleline))
                    lab_test_res.ForeColor = Color.FromArgb(255, 0, 200, 0);
                else
                    lab_test_res.ForeColor = Color.Red;
            }


        private void RgxIndexChanged(object sender, EventArgs e) {
            if(lb_rgx.SelectedIndex >= 0) {
                rgx_str = (string) bs_links_rgx[lb_rgx.SelectedIndex];
                tb_add_rgx.Text = rgx_str;
                }
            }


        private void DelRegex(object sender, EventArgs e) {
            int indx = lb_rgx.SelectedIndex;
            if(indx >= 0) {
                bs_links_rgx.RemoveAt(indx);

                if(data.LinksRegex.Count == 0) {
                    btn_del_rgx.Enabled = false;
                    }

                } else {
                lb_rgx.SelectedIndex = 0;
                }
            }

        //Save
        private void BrowseSaveFolder(object sender, EventArgs e) {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "Selecciona la carperta de salida";
            fbd.SelectedPath = data.save_folder;

            DialogResult dr = fbd.ShowDialog();

            if(( dr == DialogResult.OK || dr == DialogResult.Yes ) && fbd.SelectedPath != "") {
                string path = fbd.SelectedPath;

                if(!path.EndsWith("\\"))
                    path += "\\";

                tb_save_f.Text = path;
                SaveData();
                }
            }

        private void OpenFS(object sender, EventArgs e) {
            System.Diagnostics.Process.Start(data.save_folder);
            }

        #endregion

        void SaveData() {
            StreamWriter writer = new StreamWriter(prefs_file);
            json.WriteObject(writer.BaseStream, data);

            writer.Close();
            }

        protected override void Dispose(bool disposing) {
            if(disposing) {

                if(components != null)
                    components.Dispose();

                SaveData();

                if(data_timer != null)
                    data_timer.Dispose();

                if(controller != null && data.status != State.Iddle)
                    controller.ForceClose();

                }

            base.Dispose(disposing);
            }

        private void OnClosing(object sender, FormClosingEventArgs e) {

            if(data.status != State.Iddle) {
                DialogResult dr = MessageBox.Show("Deseas cerrar la aplicacion sin terminar las transferencias?", "Transferencia en curso", MessageBoxButtons.YesNo);

                if(dr == DialogResult.No) {
                    e.Cancel = true;
                    }

                }
            }


        }
    }
