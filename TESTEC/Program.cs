using Abot2.Crawler;
using Abot2.Poco;
using AngleSharp.Html.Dom;
using HtmlAgilityPack;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TidyManaged;

namespace TESTEC
{
    namespace TesteCrawler
    {
        public class Info
        {
            public string Leiloeiro { get; set; }
            public string Localizacao { get; set; }
            public string DadosImovel { get; set; }
            public string Tipo { get; set; }
            public string AreaUtil { get; set; }
            public string AreaTotal { get; set; }
            public string Valor { get; set; }

        }

        class Program
        {
            static async Task Main(string[] args)
            {
                Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Information()
                   .WriteTo.Console()
                   .CreateLogger();

                Log.Logger.Information("Demo starting up!");

                await DemoCrawler();

                //terminou o craw
                //começar o scraping com HTML Agility
                await DemoScrapping();

                Console.ReadKey();
            }

            private static async Task DemoCrawler()
            {
                var config = new CrawlConfiguration
                {
                    MaxPagesToCrawl = 2, //Only crawl 10 pages
                    MinCrawlDelayPerDomainMilliSeconds = 3000, //Wait this many millisecs between requests
                    IsUriRecrawlingEnabled = false
                };
                var crawler = new PoliteWebCrawler(config);

                /*crawler.ShouldCrawlPageDecisionMaker = (pageToCrawl, crawlContext) =>
                {
                    var decision = new CrawlDecision { Allow = true };
                    if (pageToCrawl.Uri.Authority == "google.com")
                        return new CrawlDecision { Allow = false, Reason = "Dont want to crawl google pages" };

                    return decision;
                };*/



                crawler.ShouldCrawlPageDecisionMaker = (pageToCrawl, crawlContext) =>
                {
                    var decision = new CrawlDecision { Allow = true };
                    if (!pageToCrawl.Uri.AbsoluteUri.Contains("leilao-de-imoveis/sp/sao-paulo/todos-os-bairros/residenciais"))
                        return new CrawlDecision { Allow = false, Reason = "Dont want to crawl google pages" };

                    return decision;
                };

                /*crawler.ShouldCrawlPageLinksDecisionMaker = (crawledPage, crawlContext) =>
                {
                    var decision = new CrawlDecision { Allow = true };
                    if (crawledPage.Content.Bytes.Length < 100)
                        return new CrawlDecision { Allow = false, Reason = "Just crawl links in pages that have at least 100 bytes" };

                    return decision;
                };*/

                //teste para pegar apenas anúncios?
                /*crawler.ShouldCrawlPageLinksDecisionMaker = (crawledPage, crawContext) =>
                {
                    Console.WriteLine(crawledPage.Uri.ToString());
                    var decision = new CrawlDecision { Allow = false };
                    if (crawledPage.Uri.ToString().Contains("leilao-de-imoveis/sp/sao-paulo/todos-os-bairros/residenciais?pagina"))
                        return new CrawlDecision { Allow = true, Reason = "Just crawl links in pages that have at least 100 bytes" };

                    return decision;
                };*/

                //events
                /*crawler.PageCrawlStarting += crawler_ProcessPageCrawlStarting;
                crawler.PageCrawlCompleted += crawler_ProcessPageCrawlCompleted;
                crawler.PageCrawlDisallowed += crawler_PageCrawlDisallowed;
                crawler.PageLinksCrawlDisallowed += crawler_PageLinksCrawlDisallowed;*/

                crawler.PageCrawlCompleted += PageCrawlCompleted;//Several events available...

                var crawlResult = await crawler.CrawlAsync(new Uri("https://www.zukerman.com.br/leilao-de-imoveis/sp/sao-paulo/todos-os-bairros/residenciais"));
            }

            private static void PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
            {
                var httpStatus = e.CrawledPage.HttpResponseMessage.StatusCode;
                var rawPageText = e.CrawledPage.Content.Text;

                var crawledPage = e.CrawledPage;
                var crawlContext = e.CrawlContext;

                var document = crawledPage.AngleSharpHtmlDocument;
                var anchors = document.QuerySelectorAll("a").OfType<IHtmlAnchorElement>();
                var hrefs = anchors.Select(x => x.Href).ToList();

                var regEx = new Regex(@"\b(apartamento|casa|residencia)\b");
                var resultList = hrefs.Where(f => regEx.IsMatch(f)).ToList();


                ParaExtrair.links.AddRange(resultList);

                ParaExtrair.links = ParaExtrair.links.Distinct().ToList();
            }

            #region events
            /*static void crawler_ProcessPageCrawlStarting(object sender, PageCrawlStartingArgs e)
            {
                PageToCrawl pageToCrawl = e.PageToCrawl;
                Console.WriteLine($"About to crawl link {pageToCrawl.Uri.AbsoluteUri} which was found on page {pageToCrawl.ParentUri.AbsoluteUri}");
            }

            static void crawler_ProcessPageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
            {
                CrawledPage crawledPage = e.CrawledPage;
                if (crawledPage.HttpRequestException != null || crawledPage.HttpResponseMessage.StatusCode != HttpStatusCode.OK)
                    Console.WriteLine($"Crawl of page failed {crawledPage.Uri.AbsoluteUri}");
                else
                    Console.WriteLine($"Crawl of page succeeded {crawledPage.Uri.AbsoluteUri}");

                if (string.IsNullOrEmpty(crawledPage.Content.Text))
                    Console.WriteLine($"Page had no content {crawledPage.Uri.AbsoluteUri}");

                var angleSharpHtmlDocument = crawledPage.AngleSharpHtmlDocument; //AngleSharp parser
            }

            static void crawler_PageLinksCrawlDisallowed(object sender, PageLinksCrawlDisallowedArgs e)
            {
                CrawledPage crawledPage = e.CrawledPage;
                Console.WriteLine($"Did not crawl the links on page {crawledPage.Uri.AbsoluteUri} due to {e.DisallowedReason}");
            }

            static void crawler_PageCrawlDisallowed(object sender, PageCrawlDisallowedArgs e)
            {
                PageToCrawl pageToCrawl = e.PageToCrawl;
                Console.WriteLine($"Did not crawl page {pageToCrawl.Uri.AbsoluteUri} due to {e.DisallowedReason}");
            }*/
            #endregion


            public static class ParaExtrair
            {
                public static List<string> links = new List<string>();
            }

            private static async Task DemoScrapping()
            {
                List<Info> infos = new List<Info>();
                foreach (string url in ParaExtrair.links)
                {
                    using (var client = new HttpClient())
                    {
                        //request página
                        client.DefaultRequestHeaders.Add("User-Agent", "C# App");
                        var html = await client.GetStringAsync(url);

                        //arrumar html
                        /*using (Document docTidy = Document.FromString(html))
                        {
                            docTidy.OutputBodyOnly = AutoBool.Yes;
                            docTidy.Quiet = true;
                            docTidy.CleanAndRepair();
                            string teste = docTidy.Save();
                            // Console.WriteLine(html);
                        }*/

                        //extrair infos
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);


                        if (false)// doc.ParseErrors != null && doc.ParseErrors.Count() > 0)
                        {
                        }
                        else
                        {

                            try
                            {
                                var count = doc.DocumentNode.SelectNodes("//div[@class='s-d-ld-i-main']").Count();
                                for (var i = 0; i < count; i++)
                                {
                                    if (doc.DocumentNode != null)
                                    {
                                        var teste = doc.DocumentNode.SelectSingleNode("//*[text()[contains(.,'Endereço:')]]").ParentNode;
                                        infos.Add(new Info
                                        {
                                            Leiloeiro = url,
                                            Localizacao = doc.DocumentNode.SelectSingleNode("//*[text()[contains(.,'Endereço:')]]").ParentNode.InnerText,
                                            DadosImovel = doc.DocumentNode.SelectSingleNode("//div[@id='divdesc']")?.InnerText,
                                            Tipo = doc.DocumentNode.SelectSingleNode("//*[text()[contains(.,'Tipo:')]]").ParentNode.InnerText,
                                            AreaUtil = doc.DocumentNode.SelectSingleNode("//*[text()[contains(.,'Área Útil:')]]").ParentNode.InnerText,
                                            AreaTotal = doc.DocumentNode.SelectSingleNode("//*[text()[contains(.,'Área Total:')]]").ParentNode.InnerText,
                                            Valor = null,
                                            //CNPJ = doc.DocumentNode.SelectNodes("//div[@id='REND_100']//table[@class='total_ise']//tbody//tr//td[@class='col1_2_ise']")[i].ChildNodes[0].InnerHtml,
                                            //Empresa = doc.DocumentNode.SelectNodes("//div[@id='REND_100']//table[@class='total_ise']//tbody//tr//td[@class='col3_4_5_6_7_8_ise']//a")[i].Attributes["title"].Value,
                                            //Dirf = DateTime.Parse(doc.DocumentNode.SelectNodes("//div[@id='REND_100']//table[@class='total_ise']//tbody//tr//td[@class='col9_10_ise']")[i].ChildNodes[0].InnerHtml)

                                        });
                                        //foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//div[@id='REND_100']//table[@class='total_ise']//tbody//tr//td[@class='col1_2_ise']"))
                                        //{
                                        //    if (node.ChildNodes[0].InnerHtml != "&nbsp;")
                                        //    {
                                        //        Console.WriteLine(node.ChildNodes[0].InnerHtml);
                                        //    }
                                        //}

                                        //foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//div[@id='REND_100']//table[@class='total_ise']//tbody//tr//td[@class='col3_4_5_6_7_8_ise']//span"))
                                        //{
                                        //    if (node.ChildNodes[0].InnerHtml != "&nbsp;")
                                        //    {
                                        //        Console.WriteLine(node.ChildNodes[0].InnerHtml);
                                        //    }
                                        //}

                                        //foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//div[@id='REND_100']//table[@class='total_ise']//tbody//tr//td[@class='col9_10_ise']"))
                                        //{
                                        //    if (node.ChildNodes[0].InnerHtml != "&nbsp;")
                                        //    {
                                        //        Console.WriteLine(node.ChildNodes[0].InnerHtml);
                                        //    }
                                        //}
                                    }
                                }
                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
                }

                foreach (Info i in infos)
                {
                    Console.WriteLine(i.Leiloeiro);
                    Console.WriteLine(i.Localizacao);
                    Console.WriteLine(i.DadosImovel);
                    Console.WriteLine(i.Tipo);
                    Console.WriteLine(i.Leiloeiro);
                    Console.WriteLine(i.AreaUtil);
                    Console.WriteLine(i.AreaTotal);
                    Console.WriteLine(i.Valor);
                }
            }
        }
    }

}
