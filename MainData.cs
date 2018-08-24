using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.ComponentModel;

namespace Robot {

    enum State {
        Iddle,
        Paused,
        Pausing,
        Working,
        NoWork
        }

    enum NavDirection {
        In,
        Out,
        Both,
        Static
        }

    enum UrlStatus {
        Todo,   //Se llama a PageProc
        Iprg,   //No existe y se comienza a descargar
        Saved,  //Termino en PageProc
        Failed, //Fallo
        Free    //Ninguno de los anteriores
        }

    [Flags]
    enum Result {
        Fail = 1,
        Exists = 2,
        IsAsset = 3,
        Ok = 4
        }

    enum Type {
        html,
        css,
        script,
        media,
        video,
        audio,
        image,
        other
        }

    struct StatusReport {
        public UrlStatus url_status;
        public Page page;
        public Asset asset;
        public int index;
        }

    class IncrementCount {

        public IncrementCount(string path) {
            this.url = path;
            }

        public string url;
        public int increment = 1;
        }

    [DataContract]
    class MainData {

        Mutex mutex;

        State intr_status = State.Iddle;

        public delegate void status_chng(State status);
        public event status_chng StatusChange;

        [DataMember]
        public List<string> org_links;
        [DataMember]
        public NavDirection nav_direction;
        [DataMember]
        public bool hnav;

        [DataMember]
        public int worker_count;
        public List<WorkerThread> workers;

        [DataMember]
        public List<string> links_regex;
        
        [DataMember]
        public int max_ext;
        [DataMember]
        public int max_int;

        [DataMember]
        public int max_size_html;
        [DataMember]
        public int timeout_html;

        [DataMember]
        public string user_agent;
        [DataMember]
        public string accept_lang;

        [DataMember]
        public string save_folder;
        [DataMember]
        public int fs_retry;

        public List<Page> links_todo;
        public List<Page> links_iprg;
        public List<Page> links_saved;
        public List<string> links_failed;

        List<Asset> assets_iprg;
        List<Asset> assets_saved;
        List<string> assets_failed;

        List<IncrementCount> file_incr;

        public delegate void NewWorkDlg(Page work);
        public event NewWorkDlg NewWork;

        public MainData() {
            Initialize();
            }

        public void Initialize() {
            mutex = new Mutex(false, "ROBOT_MUTEX_MAIN_DATA");

            links_todo = new List<Page>();
            links_iprg = new List<Page>();
            links_saved = new List<Page>();
            links_failed = new List<string>();

            assets_iprg = new List<Asset>();
            assets_saved = new List<Asset>();
            assets_failed = new List<string>();

            file_incr = new List<IncrementCount>();
            }

        public void SetUp() {
            org_links = new List<string>();
            nav_direction = NavDirection.In;
            hnav = true;

            worker_count = 3;

            links_regex = new List<string>();

            max_ext = 0;
            max_int = 0;

            max_size_html = 100000000;
            timeout_html = 10000;

            user_agent = "Robot";
            accept_lang = "en,es*";

            save_folder = "D:\\robot\\";
            fs_retry = 10;
            }

        void Block() {
            mutex.WaitOne();
            }

        void Release() {
            mutex.ReleaseMutex();
            }

        #region Links Management

        bool LinksCoincidence(Page page, URL url) {

            URL.AbsURL org_url = page.org_url.url_main;
            URL.AbsURL final_url = page.final_url.url_main;

            string org = org_url.scheme + "://" + org_url.host + org_url.path + org_url.file + org_url.query;
            string final = final_url.scheme + "://" + final_url.host + final_url.path + final_url.file + final_url.query;

            string str_url = url.url_main.scheme + "://" + url.url_main.host
                + url.url_main.path + url.url_main.file + url.url_main.query;

            return org == str_url || final == str_url;
            }

        public bool LinksEquals(URL url, URL url2) {

            URL.AbsURL oneh = url.url_main;
            URL.AbsURL twoh = url2.url_main;

            string one = oneh.scheme + "://" + oneh.host + oneh.path + oneh.file + oneh.query;
            string two = twoh.scheme + "://" + twoh.host + twoh.path + twoh.file + twoh.query;

            return one == two;
            }

        StatusReport IntrCheckLink(URL url) {
            StatusReport report = new StatusReport();

            foreach(Page page in links_todo) {
                if(LinksCoincidence(page, url)) {
                    report.url_status = UrlStatus.Todo;
                    report.page = page;
                    report.index = links_todo.IndexOf(page);
                    return report;
                    }
                }

            foreach(Page page in links_saved) {
                if(LinksCoincidence(page, url)) {
                    report.url_status = UrlStatus.Saved;
                    report.page = page;
                    report.index = links_saved.IndexOf(page);
                    return report;
                    }
                }


            foreach(Page page in links_iprg) {
                if(LinksCoincidence(page, url)) {
                    report.url_status = UrlStatus.Iprg;
                    report.page = page;
                    report.index = links_iprg.IndexOf(page);
                    return report;
                    }
                }


            foreach(string page in links_failed) {
                if(page == url.str) {
                    report.url_status = UrlStatus.Failed;
                    report.index = links_failed.IndexOf(page);
                    return report;
                    }
                }

            report.url_status = UrlStatus.Free;
            return report;
            }

        public StatusReport CheckLink(URL url) {
            StatusReport report;
            Block();
            report = IntrCheckLink(url);
            Release();
            return report;
            }

        public bool LinkStatus(Page page, UrlStatus new_status) {
            StatusReport report;
            Block();

            if(page.final_url != null) {
                report = IntrCheckLink(page.final_url);

                if(report.url_status == UrlStatus.Free)
                    report = IntrCheckLink(page.org_url);

                } else {
                report = IntrCheckLink(page.org_url);
                }

            if(report.url_status == new_status) {
                Release();
                return false;
                }

            switch(report.url_status) {
                case UrlStatus.Todo:
                    links_todo.RemoveAt(report.index);
                    break;
                case UrlStatus.Iprg:
                    links_iprg.RemoveAt(report.index);
                    break;
                case UrlStatus.Saved:
                    links_saved.RemoveAt(report.index);
                    break;
                case UrlStatus.Failed:
                    links_failed.RemoveAt(report.index);
                    break;
                }

            switch(new_status) {
                case UrlStatus.Todo:
                    links_todo.Add(page);
                    NewWork.Invoke(page);
                    break;
                case UrlStatus.Iprg:
                    links_iprg.Add(page);
                    break;
                case UrlStatus.Saved:
                    links_saved.Add(page);
                    break;
                case UrlStatus.Failed:
                    links_failed.Add(page.org_url.str);
                    break;
                }
            Release();
            return true;
            }

        #endregion

        #region Assets Management

        public bool AssetsCoincidence(Asset asset, URL url) {
            URL.AbsURL org_url = asset.org_url.url_main;
            URL.AbsURL final_url = asset.final_url.url_main;

            string org = org_url.scheme + "://" + org_url.host + org_url.path + org_url.file + org_url.query;
            string final = final_url.scheme + "://" + final_url.host + final_url.path + final_url.file + final_url.query;

            string str_url = url.url_main.scheme + "://" + url.url_main.host
                + url.url_main.path + url.url_main.file + url.url_main.query;

            return org == str_url || final == str_url;
            }

        StatusReport IntrCheckAsset(URL url) {

            StatusReport report = new StatusReport();

            foreach(Asset asset in assets_iprg) {
                if(AssetsCoincidence(asset, url)) {
                    report.url_status = UrlStatus.Iprg;
                    report.asset = asset;
                    report.index = assets_iprg.IndexOf(asset);
                    return report;
                    }
                }

            foreach(Asset asset in assets_saved) {
                if(AssetsCoincidence(asset, url)) {
                    report.url_status = UrlStatus.Saved;
                    report.asset = asset;
                    report.index = assets_saved.IndexOf(asset);
                    return report;
                    }
                }

            foreach(string asset in assets_failed) {
                if(asset == url.str) {
                    report.url_status = UrlStatus.Failed;
                    report.index = assets_failed.IndexOf(asset);
                    return report;
                    }
                }

            report.url_status = UrlStatus.Free;
            return report;
            }

        public StatusReport CheckAsset(URL url) {
            StatusReport report;
            Block();
            report = IntrCheckAsset(url);
            Release();
            return report;
            }

        public bool AssetStatus(Asset asset, UrlStatus new_status) {
            Block();

            StatusReport report;
            if(asset.final_url != null) {
                report = IntrCheckAsset(asset.final_url);

                if(report.url_status == UrlStatus.Free)
                    report = IntrCheckAsset(asset.org_url);

                } else {
                report = IntrCheckAsset(asset.org_url);
                }

            if(report.url_status == new_status) {
                Release();
                return false;
                }

            switch(report.url_status) {
                case UrlStatus.Iprg:
                    assets_iprg.RemoveAt(report.index);
                    break;
                case UrlStatus.Saved:
                    assets_saved.RemoveAt(report.index);
                    break;
                case UrlStatus.Failed:
                    assets_failed.RemoveAt(report.index);
                    break;
                }

            switch(new_status) {
                case UrlStatus.Iprg:
                    assets_iprg.Add(asset);
                    break;
                case UrlStatus.Saved:
                    assets_saved.Add(asset);
                    break;
                case UrlStatus.Failed:
                    assets_failed.Add(asset.org_url.str);
                    break;
                }

            Release();
            return true;
            }

        #endregion

        #region Properties
        public int WorkerCount {
            get { return worker_count; }
            set { worker_count = value; }
            }

        public List<string> OrgLinks {
            get { return org_links; }
            set { org_links = value; }
            }

        public int IntNavDirection {
            get { return (int) nav_direction; }
            set { nav_direction = (NavDirection) value; }

            }

        public bool HNav {
            get { return !hnav; }
            set { hnav = !value; }
            }

        public int MaxExt {
            get { return max_ext; }
            set { max_ext = value; }
            }

        public int MaxInt {
            get { return max_int; }
            set { max_int = value; }
            }

        public List<string> LinksRegex {
            get { return links_regex; }
            set { links_regex = value; }
            }

        public string AcceptLang {
            get { return accept_lang; }
            set { accept_lang = value; }
            }

        public string UserAgent {
            get { return user_agent; }
            set { user_agent = value; }
            }

        public int MaxSizeHtml {
            get { return max_size_html / 1000; }
            set { max_size_html = value * 1000; }
            }
        public int TimeoutHtml {
            get { return timeout_html / 1000; }
            set { timeout_html = value * 1000; }
            }

        public string SaveFolder {
            get { return save_folder; }
            set {
                string path = value;
                if(!path.EndsWith("\\"))
                    path += "\\";

                save_folder = path;
                }
            }

        public int FsRetry {
            get { return fs_retry; }
            set { fs_retry = value; }
            }

        #endregion

        public void AddFailed(string url) {
            Block();
            links_failed.Add(url);
            Release();
            }

        public State status {
            get {
                return intr_status;
                }

            set {
                Block();
                intr_status = value;
                Release();
                if(StatusChange != null)
                    StatusChange.Invoke(intr_status);
                }
            }

        public int GetIncrement(string url) {
            Block();
            foreach(IncrementCount ic in file_incr) {
                if(url == ic.url) {
                    Release();
                    return ic.increment++;
                    }
                }

            file_incr.Add(new IncrementCount(url));
            Release();
            return 1;
            }

        public void Reset() {
            Block();

            links_todo.Clear();
            links_iprg.Clear();
            links_saved.Clear();
            links_failed.Clear();

            assets_iprg.Clear();
            assets_saved.Clear();
            assets_failed.Clear();

            file_incr.Clear();

            Release();
            }

        public void Log(string data) {
            //Debug.WriteLine(data);
            }

        public void Log2(string data) {
            Debug.WriteLine(data);
            }

        }
    }
