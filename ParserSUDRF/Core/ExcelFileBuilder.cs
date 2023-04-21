using ParserSUDRF.Models;
using Tools.Xlsx;

namespace ParserSUDRF.Core;

public class ExcelFileBuilder : IDisposable
{
    private readonly XlsxWriter _xlsxWriter;
    private readonly Stream _memoryStream;

    public ExcelFileBuilder()
    {
        _memoryStream = new MemoryStream();
        _xlsxWriter = new XlsxWriter(_memoryStream);
    }

    public async Task SaveFile()
    {
        Parser parser = new Parser();
        
        _xlsxWriter.AddHeaderRow<CourtInfo>();
        await foreach (CourtInfo item in parser.ParseCourtInfos())
        {
            _xlsxWriter.Append(item);
        }
        
        _xlsxWriter.Save();
        
        _memoryStream.Seek(0, SeekOrigin.Begin);

        using (FileStream fileStream = new FileStream("output.xlsx", FileMode.Create))
        {
            byte[] buffer = new byte[8192]; // Размер буфера - 8 Кб
            int bytesRead;
            while ((bytesRead = _memoryStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                fileStream.Write(buffer, 0, bytesRead);
            }
        }

        Dispose();
    }

    public void Dispose()
    {
        _xlsxWriter.Dispose();
        _memoryStream.Dispose();
    }
}