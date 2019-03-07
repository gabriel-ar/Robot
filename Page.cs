using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Robot {
    class Page {

        MainData data;

        public URL org_url;
        public int org_link;
        bool keep;

        public URL final_url;
        public string str_resp;

        public Result result;
        public string file;
        public string filename;

        public Page(ref MainData main_data, URL url, int org_link, bool keep_loaded = false) {
            data = main_data;
            org_url = url;
            keep = keep_loaded;
            this.org_link = org_link;
            }

        public void Proccess() {
            try {
                result = Download();
                } catch {
                result = Result.Fail;
                return;
                }

            if(result == Result.Ok) {
                SaveFile();
                } else {
                data.Log("--->PAGE " + result.ToString() + ": " + org_url.str);
                }
            }

        Result Download() {
            //TODO Get request configs by preferences
            data.Log("--->PAGE Iniciando: " + org_url.str);

            WebHeaderCollection headers = new WebHeaderCollection {
                { HttpRequestHeader.AcceptLanguage, data.accept_lang }
            };

            HttpWebRequest request = WebRequest.CreateHttp(org_url.str);
            request.Headers = headers;
            request.Method = "GET";
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/*,*/*;q=0.8";
            request.UserAgent = data.user_agent;
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 10;
            request.Timeout = data.timeout_html;

            data.Log("--->PAGE Descargando: " + org_url.str);

            HttpWebResponse response = (HttpWebResponse) request.GetResponse();

            final_url = new URL(response.ResponseUri.OriginalString);

            foreach(string rgx in data.links_regex) {
                if(Regex.IsMatch(final_url.str, rgx))
                    return Result.Exists;
                }

            //HTML?
            if(!(response.ContentType.Contains("text/html")
                ||response.ContentType.Contains("application/") )) {
                response.Close();
                return Result.IsAsset;
                }

            //SIZE
            if(response.ContentLength > data.max_size_html && data.max_size_html > 0) {
                response.Close();
                return Result.Fail;
                }


            //FREE?
            StatusReport status = data.CheckLink(final_url);

            switch(status.url_status) {
                case UrlStatus.Iprg:
                case UrlStatus.ToDo:
                case UrlStatus.Saved:
                    ok_exists:
                    response.Close();

                    final_url = status.page.final_url;
                    file = status.page.file;
                    filename = status.page.file;

                    return Result.Ok | Result.Exists;

                case UrlStatus.Failed:
                    response.Close();
                    return Result.Fail | Result.Exists;

                case UrlStatus.Free:
                    lock (data)
                        if(!data.UpdateStatus(this, UrlStatus.Iprg))
                            goto ok_exists;

                    str_resp = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    response.Close();
                    MakeFullPath();
                    return Result.Ok;

                }
            return Result.Fail;
            }

        void MakeFullPath() {

            string s_path = final_url.url_main.host + final_url.url_main.path;
            s_path = s_path.Replace(":", ".");

            string folder = data.save_folder + s_path;
            folder = folder.Replace("/", "\\");

            filename = final_url.url_main.file;
            if(filename == null || filename == "") {
                filename = "index";
                } else {
                Regex rgx = new Regex("(?<name>.*?)(\\.[^.]*?)?$");
                filename = rgx.Match(filename).Groups["name"].Value;
                }

            file = folder + filename;
            
            if(File.Exists(file + ".html")) {
                filename += data.GetIncrement(folder);
                }

            filename += ".html";
            file = folder + filename;
            Directory.CreateDirectory(folder);
            }

        public string PathFromRelative(URL rlt_to) {

            string folder = final_url.RelativeTo(rlt_to.str).path;
            return folder + filename;
            }

        public bool SaveFile(string content = null) {

            if(content == null)
                if(str_resp != null) {
                    content = str_resp;
                    } else {
                    return false;
                    }

            Exception laste;

            int fail_count = 0;
            do {
                try {
                    File.WriteAllText(file, content);
                    if(!keep)
                        str_resp = null;

                    data.Log("--->PAGE SaveFile: " + file);
                    return true;
                    } catch (Exception e) {
                    laste = e;
                    fail_count++;
                    Thread.Sleep(2000);
                    }
                } while(fail_count > 0 && fail_count < data.fs_retry);

            data.Log("--->PAGE SaveFile FAIL: " + file);
            return false;
            }

        public string GetFile() {

            if(str_resp != null && keep) {
                return str_resp;

                } else {

                int fail_count = 0;
                do {
                    try {
                        return File.ReadAllText(file);
                        } catch {
                        fail_count++;
                        Thread.Sleep(2000);
                        }
                    } while(fail_count > 0 && fail_count < data.fs_retry);

                return null;
                }
            }

        }
    }