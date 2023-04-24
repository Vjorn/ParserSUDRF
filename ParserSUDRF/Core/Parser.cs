using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json;
using ParserSUDRF.Models;
using ParserSUDRF.Models.Json;

namespace ParserSUDRF.Core;

public class Parser
{
    private readonly string _regionsParseUrl = "https://sudrf.ru/index.php?id=300&var=true";
    private readonly string _cookieValue = "";

    public async IAsyncEnumerable<CourtInfo> ParseExistingFlag()
    {
        List<CourtInfo> courtInfosInEjSudrfList = new List<CourtInfo>();
        await foreach (CourtInfo item in ParseCourtInfosInEjSudrf())
        {
            courtInfosInEjSudrfList.Add(item);
        }
        
        await foreach (CourtInfo item in ParseCourtInfosInSudrf())
        {
            CourtInfo? courtInfoInEjSudrf = courtInfosInEjSudrfList.FirstOrDefault(e => e.Code == item.Code);
            item.EjCode = courtInfoInEjSudrf == null ? "Нет" : courtInfoInEjSudrf.Code;
            
            yield return item;
        }
    }

    public async IAsyncEnumerable<CourtInfo> ParseCourtInfosInSudrf()
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

    public async IAsyncEnumerable<CourtInfo> ParseCourtInfosInEjSudrf()
    {
        IReadOnlyDictionary<string, string> regions = await GetRegions();
        
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        HttpClientHandler handler = new HttpClientHandler();
        handler.UseCookies = true;
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        
        CookieContainer cookieContainer = new CookieContainer();
        Cookie authCookie = new Cookie("PHPSESSID", _cookieValue, "/", "ej.sudrf.ru");
        cookieContainer.Add(authCookie);
        handler.CookieContainer = cookieContainer;
        
        using (HttpClient httpClient = new HttpClient(handler))
        {
            foreach (KeyValuePair<string, string> region in regions)
            {
                string url = $"https://ej.sudrf.ru/api/appeal/getCourtsInRegion?regionCode={region.Key}";
                HttpResponseMessage courtsResponse = await httpClient.GetAsync(url);

                if (courtsResponse.IsSuccessStatusCode)
                {
                    string courtsResponseString = await courtsResponse.Content.ReadAsStringAsync();

                    Response courts = JsonConvert.DeserializeObject<Response>(courtsResponseString)!;
                    
                    foreach (Court court in courts.data.Where(e => e.ZNACHATR.Contains("участ") || e.ZNACHATR.Contains("миров")))
                    {
                        CourtInfo courtInfo = new CourtInfo();
                        courtInfo.Region = region.Value;
                        courtInfo.Name = court.ZNACHATR;
                        courtInfo.Code = court.VNKOD;

                        yield return courtInfo;
                    }
                }
            }
        }
    }
    
    private async IAsyncEnumerable<CourtInfo> GetCourtInfos(HttpClient httpClient)
    {
        IReadOnlyDictionary<string, string> regions = await GetRegions();

        foreach (KeyValuePair<string, string> region in regions)
        {
            string pageUrl = $"https://sudrf.ru/index.php?id=300&act=go_ms_search&searchtype=ms&var=true&ms_type=ms&court_subj={region.Key}&ms_city=&ms_street=";

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
                        courtInfo.Region = region.Value;
                        courtInfo.Name = nameNode.InnerText.Trim();
                        courtInfo.Code = codeTextNode.InnerText.Trim();
                        courtInfo.Website = websiteNode.InnerText.Trim();

                        yield return courtInfo;
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
                
            Regex addressRegex =
                new Regex(@"balloons_user\['(.*?)'\]\[.*?\]=\{type:'mir',name:'(.*?)',adress:'(.*?)',coord:.*?};",
                    RegexOptions.Compiled);
                
            MatchCollection matches = addressRegex.Matches(yandexCoordsContentString);

            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                string address = match.Groups[3].Value;

                addressCodeMap.Add(key, address);
            }
        }
        
        return addressCodeMap;
    }
    
    private async Task<IReadOnlyDictionary<string, string>> GetRegions()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        
        using (HttpClient httpClient = new HttpClient(new HttpClientHandler
                   { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }))
        {
            string url = _regionsParseUrl;
            HttpResponseMessage regionsResponse = await httpClient.GetAsync(url);

            Dictionary<string, string> regionsDictionary = new Dictionary<string, string>();
            
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
                    
                    regionsDictionary.Add($"{numericValue:00}", optionNode.InnerText.Trim());
                }

                return regionsDictionary;
            }

            throw new Exception("Что-то сломалось: Регионы не считаны");
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