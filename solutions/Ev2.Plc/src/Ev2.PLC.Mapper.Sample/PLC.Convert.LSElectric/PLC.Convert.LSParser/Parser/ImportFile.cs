using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Provides methods for reading files and returning their contents as a list of strings.
/// </summary>
public static class ImportFile
{
    /// <summary>
    /// Reads a file line by line and returns the contents as a list of strings.
    /// </summary>
    /// <param name="filePath">The path to the file to be read.</param>
    /// <returns>A list of strings representing each line of the file.</returns>
    /// <exception cref="Exception">Thrown when the file path does not exist.</exception>
    public static IEnumerable<string> ReadFileByLine(string filePath)
    {
        List<string> lines = new();
        if (!File.Exists(filePath))
        {
            throw new Exception($"Could not find a part of the path {filePath}");
        }

        try
        {
            int euckrCodePage = 51949;  // euc-kr 코드 번호
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            System.Text.Encoding eucKr = System.Text.Encoding.GetEncoding(euckrCodePage);

            //Encoding eucKr = Encoding.GetEncoding(euckrCodePage);
            using StreamReader reader = new(filePath, eucKr); // Reading the file with EUC-KR encoding
            string line;
            while ((line = reader.ReadLine()) != null) // Reading the file until the end
            {
                lines.Add(line);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
            // 오류 발생 시 빈 리스트 반환 가능
        }

        return lines;
    }

    /// <summary>
    /// Reads all lines of a file using the default encoding and returns them as a list of strings.
    /// </summary>
    /// <param name="file">The path to the file to be read.</param>
    /// <returns>A list of strings representing each line of the file.</returns>
    public static IEnumerable<string> Read(string file)
    {
        return File.ReadAllLines(file, Encoding.Default);
    }
}
