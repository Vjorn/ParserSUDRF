using Tools.Xlsx;

namespace ParserSUDRF.Models;

public sealed class CourtInfo
{
    [Name("Регион суда")]
    public string? Region { get; set; }
    
    [Name("Наименование суда")]
    public string? Name { get; set; }
    
    [Name("Код суда")]
    public string? Code { get; set; }
    
    [Name("Веб-сайт")]
    public string? Website { get; set; }
    
    [Name("Адрес суда")]
    public string? Address { get; set; }
}