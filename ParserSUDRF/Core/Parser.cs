using System.Net;
using System.Text;
using HtmlAgilityPack;

namespace ParserSUDRF.Core;

public class Parser
{
    private readonly string _baseUrl;

    public Parser(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public async Task<Dictionary<string, string>> ParseRegions()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using (HttpClient httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
        {
            string url = _baseUrl;
            HttpResponseMessage response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
                string contentString = Encoding.GetEncoding("windows-1251").GetString(contentBytes);

                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(contentString);

                HtmlNode? selectNode = document.DocumentNode.SelectSingleNode("//select[@id='court_subj']");
                HtmlNodeCollection optionNodes = selectNode.SelectNodes("./option");

                Dictionary<string, string> courtSubjDict = new Dictionary<string, string>();
                foreach (HtmlNode? optionNode in optionNodes)
                {
                    string? value = optionNode.GetAttributeValue("value", "");

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        int numericValue = Convert.ToInt32(value);
                        courtSubjDict.Add($"{numericValue:00}", optionNode.InnerText.Trim());
                    }
                }

                return courtSubjDict;
            }
            
            throw new Exception($"Не удалось получить страницу. Код ошибки: {response.StatusCode}");
        }
    }
}