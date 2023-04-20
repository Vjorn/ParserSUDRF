using System.Net;
using System.Text;
using HtmlAgilityPack;

namespace SudrfParser
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using (HttpClient httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
            {
                string url = "https://sudrf.ru/index.php?id=300&var=true";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
                    string contentString = Encoding.GetEncoding("windows-1251").GetString(contentBytes);

                    HtmlDocument document = new HtmlDocument();
                    document.LoadHtml(contentString);

                    HtmlNode? selectNode = document.DocumentNode.SelectSingleNode("//select[@id='court_subj']");
                    HtmlNodeCollection optionNodes = selectNode.SelectNodes("./option");

                    Dictionary<int, string> courtSubjDict = new Dictionary<int, string>();
                    foreach (HtmlNode? optionNode in optionNodes)
                    {
                        string? value = optionNode.GetAttributeValue("value", "");

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            courtSubjDict.Add(Convert.ToInt32(value), optionNode.InnerText.Trim());
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Не удалось получить страницу. Код ошибки: {response.StatusCode}");
                }
            }
        }
    }
}