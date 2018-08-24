using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Robot {
    public class URL {

        //TODO Make a path errors fixer

        public static string rgx_url = "(?<scheme>http|https)://(?<host>([a-zA-Z.-]+|\\d{1,3}(\\.\\d{1,3}){3})(:\\d{1,5})?)(?:(?<path>([^/?#]*?/)+)(?<file>[^/?#]*?)?(?<query>\\?.*?)?(?<goto>#[^/?#]*?)?$|$)";
        static string rgx_rel = "(?<path>([^/?#]*?/)+)?(?<file>[^/?#]*?)?(?<query>\\?.*?)?(?<goto>#[^/?#]*?)?$";
        static string rgx_path = "[^/?#]*?/|(\\.\\.$)";

        public struct AbsURL {
            public string org_str;
            public string scheme;
            public string host;
            public string path;
            public string file;
            public string nav;
            public string query;
            public bool invalid;
            }

        public struct RelURL {
            public string org_str;
            public string path;
            public string file;
            public string nav;
            public string query;
            }

        public enum NavType {
            Out,
            In,
            Side,
            OutDiff,
            InDiff,
            Diff,
            Same,
            SameQuery,
            Domain,
            }

        public AbsURL url_main;
        AbsURL parent;

        public string str {
            get {
                Recompose();
                return url_main.org_str;
                }
            }

        public URL(string url) {
            url_main = ParseAbs(url);
            url_main.org_str = url;
            }

        public URL(string abs_parent, string rel_url) {

            parent = ParseAbs(abs_parent);

            if(rel_url.StartsWith("/")) {
                string real_url = parent.scheme + "://" + parent.host + rel_url;

                url_main = ParseAbs(real_url);
                url_main.org_str = real_url;
                return;
                }

            RelURL tmp_url = ParseRel(rel_url);

            int back_rest = BackProcess(ref tmp_url.path);
            parent = ParseAbs(abs_parent, back_rest);

            string result_path = "";
            if(Regex.IsMatch(parent.path, "/$") && Regex.IsMatch(tmp_url.path, "^/")) {
                result_path = parent.path.Remove(0, parent.path.Length - 1) + tmp_url.path;
                } else if(Regex.IsMatch(parent.path, "[^/]$") && Regex.IsMatch(tmp_url.path, "^[^/]")) {
                result_path = parent.path + "/" + tmp_url.path;
                } else {
                result_path = parent.path + tmp_url.path;
                }

            string result = parent.scheme + "://" + parent.host + result_path + tmp_url.file + tmp_url.nav + tmp_url.query;

            url_main = ParseAbs(result);
            url_main.org_str = result;
            }

        public NavType Compare(string abs_parent) {
            AbsURL parent = ParseAbs(abs_parent);
            return Compare(parent, url_main);
            }

        public static NavType Compare(string abs_parent, string child) {
            AbsURL url_main = ParseAbs(child);
            AbsURL parent = ParseAbs(abs_parent);
            return Compare(parent, url_main);
            }

        static NavType Compare(AbsURL parent, AbsURL child) {

            if(parent.host != child.host)
                return NavType.Diff;

            MatchCollection mtc_main = Regex.Matches(child.path, rgx_path);
            MatchCollection mtc_parent = Regex.Matches(parent.path, rgx_path);

            int equals = 0;
            for(int c = 0; c < Math.Min(mtc_main.Count, mtc_parent.Count); c++) {
                if(mtc_main[c].Value == mtc_parent[c].Value)
                    equals++;
                }

            if(mtc_main.Count > mtc_parent.Count) {
                //Padre MENOR
                if(equals == mtc_parent.Count) {
                    return NavType.In;
                    } else if(equals < mtc_parent.Count) {
                    return NavType.InDiff;
                    }

                } else if(mtc_main.Count < mtc_parent.Count) {
                //Padre MAYOR
                if(equals == mtc_main.Count) {
                    return NavType.Out;
                    } else {
                    return NavType.OutDiff;
                    }
                } else if(mtc_main.Count == mtc_parent.Count) {
                if(equals == mtc_main.Count) {
                    if(parent.file == child.file) {
                        return NavType.Same;
                        } else if(parent.file == child.file
                        && parent.query == child.query) {
                        return NavType.SameQuery;
                        }
                    return NavType.Side;
                    } else if(equals < mtc_parent.Count) {
                    return NavType.OutDiff;
                    } else {
                    return NavType.InDiff;
                    }
                }

            return NavType.Diff;
            }

        public RelURL RelativeTo(string absolute) {
            AbsURL parent = ( new URL(absolute) ).url_main;

            MatchCollection origin = Regex.Matches(parent.path, rgx_path);
            MatchCollection destiny = Regex.Matches(url_main.path, rgx_path);

            bool fill = false;
            string result = "";

            //detecta diferentes hosts
            if(url_main.host != parent.host) {

                for(int pth = 0; pth < origin.Count; pth++) {
                    result += "../";
                    }

                result += url_main.host;
                if(url_main.path == null && url_main.path == "") {
                    result += "/";
                    }
                fill = true;
                }

            for(int c = 0; c < destiny.Count; c++) {

                //se rellena con los que quedan en destino
                if(fill) {

                    result += destiny[c].Value;

                    //origen es menor
                    //--->se completa con los que resten
                    } else if(origin.Count - 1 < c) {

                    result += destiny[c].Value;


                    //diferentes
                    //---> a partir de aqui se completa con ../ por las que resten
                    } else if(origin[c].Value != destiny[c].Value) {
                    fill = true;

                    for(int rest = 0; rest < origin.Count - c; rest++) {
                        result += "../";
                        }

                    result += destiny[c].Value;

                    }

                //origen mayor que destino
                //--->completar con ../
                if(c == destiny.Count - 1 && !fill && ( origin.Count - 1 ) - c > 0) {

                    for(int rest = 0; rest < ( origin.Count - c ) - 1; rest++) {
                        result += "../";
                        }
                    }
                }

            RelURL rel_url = new RelURL() {
                org_str = result + url_main.file + url_main.nav + url_main.query,
                path = result,
                file = url_main.file,
                nav = url_main.nav,
                query = url_main.query
                };
            return rel_url;
            }

        void Recompose() {
            url_main.org_str = url_main.scheme + "://" + url_main.host + url_main.path + url_main.file + url_main.nav + url_main.query;
            }

        public static bool IsValid(string url) {
            //TODO
            return true;
            }

        static AbsURL ParseAbs(string url, int back_count = 0) {
            AbsURL comp = new AbsURL();
            Match match = Regex.Match(url, rgx_url);

            comp.scheme = match.Groups["scheme"].Value;
            comp.host = match.Groups["host"].Value;

            if(!match.Success || comp.scheme == "" || comp.host == "")
                throw new Exception(url);

            if(match.Groups["file"].Value == "..") {
                back_count++;
                comp.file = "";
                } else if(match.Groups["file"].Value != "") {
                comp.file = match.Groups["file"].Value;
                }

            comp.path = "";
            if(match.Groups["path"].Value != "") {
                comp.path = match.Groups["path"].Value;

                if(BackProcess(ref comp.path, back_count) > 0) {
                    comp.invalid = true;
                    }
                }

            if(match.Groups["goto"].Value != "") {
                comp.nav = match.Groups["goto"].Value;
                }

            if(match.Groups["query"].Value != "") {
                comp.query = match.Groups["query"].Value;
                }

            return comp;
            }

        RelURL ParseRel(string url) {

            RelURL comp = new RelURL();
            Match match = Regex.Match(url, rgx_rel);

            if(!match.Success)
                throw new Exception("URL invalida");

            comp.path = "";
            if(match.Groups["file"].Value == "..") {
                comp.path = "..";
                comp.file = null;
                } else if(match.Groups["file"].Value != "") {
                comp.file = match.Groups["file"].Value; ;
                }

            if(match.Groups["path"].Value != "") {
                comp.path = match.Groups["path"].Value + comp.path;
                }

            if(match.Groups["goto"].Value != "") {
                comp.nav = match.Groups["goto"].Value; ;
                }

            if(match.Groups["query"].Value != "") {
                comp.query = match.Groups["query"].Value; ;
                }

            return comp;
            }

        static int BackProcess(ref string url_path, int back_count = 0) {

            string back_comp = "";

            MatchCollection matches = Regex.Matches(url_path, rgx_path);
            for(int c = matches.Count - 1; c >= 0; c--) {
                if(Regex.IsMatch(matches[c].Value, "^\\.\\./$|^\\.\\.$")) {
                    back_count++;
                    } else {
                    if(back_count == 0) {
                        back_comp = matches[c].Value + back_comp;
                        } else {
                        back_count--;
                        }
                    }
                }
            url_path = back_comp;
            return back_count;
            }

        public static bool IsRelative(string url) {
            return !Regex.IsMatch(url, rgx_url);
            }

        }

    }
