using ParserSUDRF.Core;

Parser parser = new Parser("https://sudrf.ru/index.php?id=300&var=true");
Dictionary<string, string> regions = await parser.ParseRegions();

Console.WriteLine();