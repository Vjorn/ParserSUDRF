using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ParserSUDRF.Models;

namespace ParserSUDRF.Core;

public class Parser
{
    private readonly string _regionsParseUrl = "https://sudrf.ru/index.php?id=300&var=true";
    
    public async IAsyncEnumerable<CourtInfo> ParseCourtInfos()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using (HttpClient httpClient = new HttpClient(new HttpClientHandler 
                   { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
        {
            IReadOnlyDictionary<string, string> addressMap = await GetAddressesMap(httpClient);

            await foreach (CourtInfo courtInfo in GetCourtInfos(httpClient))
            {
                if (courtInfo.Code != null && addressMap.TryGetValue(courtInfo.Code, out string? address))
                {
                    courtInfo.Address = address;
                }

                yield return courtInfo;
            }
        }
    }

    private async IAsyncEnumerable<CourtInfo> GetCourtInfos(HttpClient httpClient)
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
                        HtmlNode? websiteNode = codeNode.SelectSingleNode(".//a");
                        
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
                            courtInfo.Website = websiteNode.InnerText.Trim();

                            yield return courtInfo;
                        }
                    }
                }
            }
        }
    }
    

    private async Task<IReadOnlyDictionary<string, string>> GetAddressesMap(HttpClient httpClient)
    {
        Dictionary<string, string> addressCodeMap = new Dictionary<string, string>();
        
        string yandexCoordsUrl = "https://sudrf.ru/index.php?id=300&act=ya_coords&type_suds=mir";
        HttpResponseMessage yandexCoordsResponse = await httpClient.GetAsync(yandexCoordsUrl);
        
        if (yandexCoordsResponse.IsSuccessStatusCode)
        {
            string yandexCoordsContentString = await yandexCoordsResponse.Content.ReadAsStringAsync();
                
            string pattern = @"balloons_user\['(.*?)'\]\[.*?\]=\{type:'mir',name:'(.*?)',adress:'(.*?)',coord:.*?};";
                
            MatchCollection matches = Regex.Matches(yandexCoordsContentString, pattern);

            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                string name = match.Groups[2].Value;
                string address = match.Groups[3].Value;

                // string value = $"{name}, {address}";

                addressCodeMap.Add(key, address);
            }
        }
        
        return addressCodeMap;
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