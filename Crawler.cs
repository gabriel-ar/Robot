using System;
using System.Collections.Generic;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

/*
Toda esta clase debe de ser ejecutada en un Thread independiente
Orden y Logica de la Pagina:
1-Descargar HTML
2-Detectar enlaces y separar por tipo:
    *HTML:
        -Validación:
            Dir de Navegación o Direcciones
            Profundidad
            Tamaño
            Reglas Generales
        -Crear página temporal
        -Agregar el nuevo enlace a Hilos
        -Sustituir el enlace en la página.

    *CSS:
        -Validación:
            Extensión
            Ubicación
            Reglas Generales
            Tamaño
        -Descargar
        -Sustituir enlace de página

3-Guardar la página en disco
4-Agregar a la lista de descargadas

    Si un asset no se puede descargar se añade a la lista de Assets Fallidos 
    Si la pagina no se puede descargar se agrega a la lista de Paginas Fallidas
*/


namespace Robot {
    class Crawler {
        readonly string url_rgx = "url\\(\\\"?(?<url>.*?)\\\"?\\)";

        MainData data;
        Page page;

        HtmlDocument doc;

        List<Asset> css_todo;

        public Crawler(ref MainData main_data, Page pg) {
            data = main_data;
            page = pg;

            css_todo = new List<Asset>();

            doc = new HtmlDocument();
            doc.LoadHtml(page.GetFile());

            ParseLinks();
            ParseAssets();

            Save();
            }

        void ParseLinks() {
            data.Log("-->PROC: ParseLinks: " + page.final_url.str);
            HtmlNodeCollection links = doc.DocumentNode.SelectNodes("//a");

            if(links != null)
                foreach(HtmlNode node in links) {
                    LinkProc(node);
                    }
            }

        void LinkProc(HtmlNode node) {

            string link = node.GetAttributeValue("href", null);
            if(link != null)
                link.Trim();
            else
                return;

            //Links Regex
            foreach(string rgx in data.links_regex) {
                if(Regex.IsMatch(link, rgx))
                    return;
                }

            URL url_link;
            if(URL.IsRelative(link))
                url_link = new URL(page.final_url.str, link);
            else
                url_link = new URL(link);

            //Its same page?
            if(link == page.final_url.url_main.file) {
                node.SetAttributeValue("href", page.filename);
                return;
                }

            //Its internal?
            if(link.StartsWith("#"))
                return;

            //Nav Direction
            URL.NavType nt = url_link.Compare(data.org_links[page.org_link]);

            if(nt == URL.NavType.Same) {
                StatusReport sr = data.CheckLink(url_link);
                if(sr.page != null)
                    node.SetAttributeValue("href", sr.page.PathFromRelative(page.final_url));
                return;
                }

            switch(data.nav_direction) {

                case NavDirection.In:
                    if(nt != URL.NavType.In
                        && !( data.hnav && nt == URL.NavType.Side ))
                        return;
                    break;

                case NavDirection.Out:
                    if(nt != URL.NavType.Out
                        && !( data.hnav && nt == URL.NavType.Side ))
                        return;
                    break;

                case NavDirection.Static:
                    if(nt != URL.NavType.Side)
                        return;
                    break;

                case NavDirection.Both:
                    if(!data.hnav && nt == URL.NavType.Side)
                        break;
                    return;

                }

            //Its already dowloaded?
            StatusReport report = data.CheckLink(url_link);

            //FOR DOWNLOAD
            if(report.url_status == UrlStatus.Free) {
                data.Log("-->PROC: Link Pending");

                Page link_page = new Page(ref data, url_link, page.org_link);
                link_page.Proccess();

                switch(link_page.result) {

                    case Result.Ok:
                        string final_link = link_page.PathFromRelative(page.final_url);
                        node.SetAttributeValue("href", final_link);

                        data.LinkStatus(link_page, UrlStatus.Todo);
                        break;

                    case Result.IsAsset:
                        //TODO Do something
                        break;

                    case Result.Fail:
                        data.LinkStatus(link_page, UrlStatus.Failed);
                        break;

                    }

                //DOWNLOADED
                } else if(report.url_status != UrlStatus.Failed) {
                //TODO
                string final_link = report.page.PathFromRelative(page.final_url);
                node.SetAttributeValue("href", final_link);
                }

            }

        void ParseAssets() {
            HtmlNodeCollection items;

            //scripts
            items = doc.DocumentNode.SelectNodes("//script");
            if(items != null)
                foreach(HtmlNode item in items) {
                    string src = item.GetAttributeValue("src", null);
                    src = AssetProc(src, Type.script);
                    if(src != null)
                        item.SetAttributeValue("src", src);
                    }

            //style embedded
            items = doc.DocumentNode.SelectNodes("//style");
            if(items != null)
                foreach(HtmlNode item in items) {

                    foreach(Match match in Regex.Matches(item.InnerHtml, url_rgx)) {
                        if(!match.Success)
                            continue;

                        string str_url = match.Groups["url"].Value;
                        string url;
                        try {
                            url = AssetProc(str_url, Type.other, item.InnerHtml);
                            } catch { continue; }

                        item.InnerHtml = item.InnerHtml.Replace(match.Value, "url(\"" + url + "\")");
                        }

                    }

            //images
            items = doc.DocumentNode.SelectNodes("//img");
            if(items != null)
                foreach(HtmlNode item in items) {
                    string src = item.GetAttributeValue("src", null);
                    src = AssetProc(src, Type.image);
                    if(src != null)
                        item.SetAttributeValue("src", src);
                    }

            //link tags
            items = doc.DocumentNode.SelectNodes("//link");
            if(items != null)
                foreach(HtmlNode item in items) {

                    switch(item.GetAttributeValue("rel", "")) {
                        case "":
                            continue;

                        case "stylesheet":
                            string href = item.GetAttributeValue("href", null);
                            href = AssetProc(href, Type.css);
                            if(href != null)
                                item.SetAttributeValue("href", href);
                            break;

                        }

                    }

            //links in css: url("link")
            foreach(Asset css in css_todo) {
                foreach(Match match in Regex.Matches(css.str_resp, url_rgx)) {
                    if(match.Success) {
                        string str_url = match.Groups["url"].Value;
                        string url;
                        try {
                            url = AssetProc(str_url, Type.other, css.final_url.str);
                            } catch { continue; }

                        css.str_resp = css.str_resp.Replace(match.Value, "url(\"" + url + "\")");
                        }
                    }
                css.Save();
                }


            }

        string AssetProc(string src, Type type, string rel = null) {

            if(src == null)
                return null;

            if(rel == null)
                rel = page.final_url.str;

            URL url_src;
            if(URL.IsRelative(src))
                url_src = new URL(rel, src);
            else
                url_src = new URL(src);

            StatusReport report = data.CheckAsset(url_src);

            if(report.url_status == UrlStatus.Iprg || report.url_status == UrlStatus.Saved) {

                return report.asset.final_url.RelativeTo(rel).org_str;

                } else if(report.url_status == UrlStatus.Free) {
                Asset asset = new Asset(url_src, ref data);

                if(type == Type.css)
                    asset.Process(true);
                else
                    asset.Process();

                if(asset.result == Result.Ok || asset.result == Result.Exists) {
                    if(asset.result == Result.Ok) {
                        data.AssetStatus(asset, UrlStatus.Saved);

                        if(type == Type.css)
                            css_todo.Add(asset);
                        }

                    return asset.final_url.RelativeTo(rel).org_str;

                    } else if(asset.result == Result.Fail) {
                    data.AssetStatus(asset, UrlStatus.Failed);
                    return null;
                    }

                }
            return null;
            }

        void Save() {
            if(page.SaveFile(doc.DocumentNode.OuterHtml))
                data.LinkStatus(page, UrlStatus.Saved);
            else
                data.LinkStatus(page, UrlStatus.Failed);

            }
        }
    }
