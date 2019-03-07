using System.IO;
using System.Net;
using System.Threading;

namespace Robot {
    class Asset {

        MainData data;
        bool in_string;

        public URL org_url;

        public URL final_url;

        public Result result;
        public string file;
        public string str_resp;

        public Asset(URL url, ref MainData main_data) {
            data = main_data;
            org_url = url;
            }

        public void Process(bool in_string = false) {
            this.in_string = in_string;
            try {
                result = Download();
                } catch {
                result = Result.Fail;
                return;
                }

            if(result != Result.Ok) {
                data.Log("--->ASSET " + result.ToString() + ": " + org_url.str);
                }

            }

        Result Download() {
            HttpWebRequest request = WebRequest.CreateHttp(org_url.str);
            request.Method = "GET";
            request.Accept = "*/*";
            request.UserAgent = data.user_agent;
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 10;
            request.Timeout = 5000;

            data.Log("--->ASSET Descargando: " + org_url.str);

            HttpWebResponse response = (HttpWebResponse) request.GetResponse();

            final_url = new URL(response.ResponseUri.OriginalString);

            StatusReport report = data.CheckAsset(final_url);

            switch(report.url_status) {

                case UrlStatus.Failed:
                    return Result.Fail | Result.Exists;

                case UrlStatus.Free:
                    data.UpdateStatus(this, UrlStatus.Iprg);

                    if(!MakeFullPath())
                        return Result.Exists;

                    Stream rs = response.GetResponseStream();

                    if(in_string) {

                        str_resp = new StreamReader(rs).ReadToEnd();
                        File.WriteAllText(file, str_resp);

                        } else {
                        FileStream fs = File.OpenWrite(file);

                        byte[] buffer = new byte[response.ContentLength];
                        long copied = 0;

                        //TODO Make this bufer HD I/O friendly
                        while(copied < response.ContentLength) {

                            int readed = rs.Read(buffer, 0, buffer.Length);
                            fs.Write(buffer, 0, readed);

                            copied += readed;
                            }

                        fs.Close();
                        }

                    response.Close();
                    return Result.Ok;

                case UrlStatus.Iprg:
                case UrlStatus.Saved:
                    return Result.Exists;

                default:
                    return Result.Exists;
                }
            }

        bool MakeFullPath() {

            string s_path = final_url.url_main.host + final_url.url_main.path;
            s_path = s_path.Replace(":", ".");

            string folder = data.save_folder + s_path;
            folder = folder.Replace("/", "\\").Replace("\\\\", "\\");

            Directory.CreateDirectory(folder);

            string filename = final_url.url_main.file;

            if(filename == null || filename == "") {
                filename = data.GetIncrement(final_url.str).ToString();

                file = folder + filename;
                return true;
                }

            //TODO Mejorar este sistema, puede fallar
            file = folder + filename;
            if(File.Exists(file))
                return false;

            return true;
            }

        //Only for css files
        public void Save() {
            int fail_count = 0;
            do {
                try {
                    File.WriteAllText(file, str_resp);

                    data.Log("--->PAGE SaveFile: " + file);
                    return;
                    } catch {
                    fail_count++;
                    Thread.Sleep(2000);
                    }
                } while(fail_count > 0 && fail_count < data.fs_retry);
            }

        }
    }
