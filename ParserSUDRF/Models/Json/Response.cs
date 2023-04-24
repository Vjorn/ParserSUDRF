namespace ParserSUDRF.Models.Json;

public class Response
{
    public int error { get; set; }
    public string errorMsg { get; set; }
    public List<Court> data { get; set; }
}

public class Court
{
    public string? VNKOD { get; set; }
    public string? ZNACHATR { get; set; }
    public string? ADRESS { get; set; }
    public string? UPKOD { get; set; }
}