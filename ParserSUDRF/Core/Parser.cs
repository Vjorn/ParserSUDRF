using System.Net;
using System.Text;
using HtmlAgilityPack;
using ParserSUDRF.Models;

namespace ParserSUDRF.Core;

public class Parser
{
    private readonly string _regionsParseUrl = "https://sudrf.ru/index.php?id=300&var=true";
    
    public async IAsyncEnumerable<CourtInfo> ParseCourtInfos()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using (HttpClient httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
        {
            string url = _regionsParseUrl;
            HttpResponseMessage regionsResponse = await httpClient.GetAsync(url);

            if (regionsResponse.IsSuccessStatusCode)
            {
                byte[] regionsContentBytes = await regionsResponse.Content.ReadAsByteArrayAsync();
                string regionsContentString = Encoding.GetEncoding("windows-1251").GetString(regionsContentBytes);

                foreach (HtmlNode? optionNode in GetRegionNodes(regionsContentString))
                {
                    string? value = optionNode.GetAttributeValue("value", "");
                    int numericValue = Convert.ToInt32(value);

                    if (numericValue == 0 || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }
                    
                    string pageUrl = $"https://sudrf.ru/index.php?id=300&act=go_ms_search&searchtype=ms&var=true&ms_type=ms&court_subj={numericValue:00}&ms_city=&ms_street=";
                    
                    HttpResponseMessage courtsResponse = await httpClient.GetAsync(pageUrl);
                    
                    if (courtsResponse.IsSuccessStatusCode)
                    {
                        string courtsContentString = await courtsResponse.Content.ReadAsStringAsync();

                        foreach (HtmlNode? courtNode in GetCourtNodes(courtsContentString))
                        {
                            HtmlNode? nameNode = courtNode.SelectSingleNode("./a");
                            if (nameNode == null)
                            {
                                continue;
                            }
                            
                            HtmlNode? codeNode = courtNode.SelectSingleNode("./div[@class='courtInfoCont']");
                            HtmlNodeCollection? codeTextNodes = codeNode.SelectNodes("./text()");
                            
                            foreach (HtmlNode? codeTextNode in codeTextNodes)
                            {
                                if (string.IsNullOrWhiteSpace(codeTextNode.InnerText))
                                {
                                    continue;
                                }
                                
                                CourtInfo courtInfo = new CourtInfo();
                                courtInfo.Region = optionNode.InnerText.Trim();
                                courtInfo.Name = nameNode.InnerText.Trim();
                                courtInfo.Code = codeTextNode.InnerText.Trim();

                                yield return courtInfo;
                            }
                        }
                    }
                }
            }
        }
    }

    private HtmlNodeCollection GetRegionNodes(string regionsContentString)
    {
        HtmlDocument regionsDoc = new HtmlDocument();
        regionsDoc.LoadHtml(regionsContentString);

        HtmlNode? regionsSingleNode = regionsDoc.DocumentNode.SelectSingleNode("//select[@id='court_subj']");
        return regionsSingleNode.SelectNodes("./option");
    }
    
    private HtmlNodeCollection GetCourtNodes(string courtsContentString)
    {
        HtmlDocument courtsDoc = new HtmlDocument();
        courtsDoc.LoadHtml(courtsContentString);
                        
        HtmlNode? courtsSingleNode = courtsDoc.DocumentNode.SelectSingleNode("//table[@class='msSearchResultTbl']");
        return courtsSingleNode.SelectNodes("./tr/td");
    }
}