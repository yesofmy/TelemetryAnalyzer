using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using TelemetryAnalyzer.App.Models;

namespace TelemetryAnalyzer.App.Services;

public sealed class CsvTelemetryImporter
{
    private const int PreviewLimit = 100;

    public TelemetryImportResult Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "The selected CSV file was not found.",
                filePath);
        }

        string[] lines = File.ReadAllLines(
            filePath,
            DetectEncoding(filePath));

        if (lines.Length < 2)
        {
            throw new InvalidDataException(
                "The file must contain a header and data rows.");
        }

        char delimiter = DetectDelimiter(lines[0]);
        List<string> headers = ParseCsvLine(lines[0], delimiter);

        if (headers.Count == 0)
        {
            throw new InvalidDataException("The CSV header is empty.");
        }

        for (int i = 0; i < headers.Count; i++)
        {
            headers[i] = headers[i].Trim().Trim('\uFEFF');

            if (string.IsNullOrWhiteSpace(headers[i]))
                headers[i] = $"Column_{i + 1}";
        }

        MakeHeadersUnique(headers);

        Dictionary<string, int> columns = MapColumns(headers);

        TelemetrySession session = new()
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            SourceFile = Path.GetFullPath(filePath)
        };

        DataTable preview = new();

        foreach (string header in headers)
            preview.Columns.Add(header, typeof(string));

        int validRows = 0;
        int invalidRows = 0;
        int missingValues = 0;

        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                continue;

            List<string> fields;

            try
            {
                fields = ParseCsvLine(lines[lineIndex], delimiter);
            }
            catch (InvalidDataException)
            {
                invalidRows++;
                continue;
            }

            if (fields.Count != headers.Count)
            {
                invalidRows++;
                continue;
            }

            TelemetryRecord record = new()
            {
                Index = validRows + 1
            };

            bool hasUsableData = false;

            foreach (KeyValuePair<string, int> column in columns)
            {
                string value = fields[column.Value].Trim();

                if (string.IsNullOrWhiteSpace(value))
                {
                    missingValues++;
                    continue;
                }

                if (!TryParseNumber(value, out double number))
                {
                    invalidRows++;
                    hasUsableData = false;
                    break;
                }

                switch (column.Key)
                {
                    case "time":
                        record.Time = number;
                        hasUsableData = true;
                        break;

                    case "speed":
                        record.Speed = number;
                        hasUsableData = true;
                        break;

                    case "rpm":
                        record.RPM = number;
                        hasUsableData = true;
                        break;

                    case "gear":
                        if (number != Math.Truncate(number) ||
                            number < int.MinValue ||
                            number > int.MaxValue)
                        {
                            hasUsableData = false;
                            invalidRows++;
                            goto InvalidRow;
                        }

                        record.Gear = (int)number;
                        hasUsableData = true;
                        break;

                    case "throttle":
                        record.Throttle = number;
                        hasUsableData = true;
                        break;

                    case "brake":
                        record.Brake = number;
                        hasUsableData = true;
                        break;

                    case "distance":
                        record.Distance = number;
                        hasUsableData = true;
                        break;

                    case "latitude":
                        record.Latitude = number;
                        hasUsableData = true;
                        break;

                    case "longitude":
                        record.Longitude = number;
                        hasUsableData = true;
                        break;
                }
            }

            InvalidRow:

            if (!hasUsableData)
                continue;

            session.Records.Add(record);
            validRows++;

            if (preview.Rows.Count < PreviewLimit)
            {
                DataRow previewRow = preview.NewRow();

                for (int i = 0; i < headers.Count; i++)
                    previewRow[i] = fields[i].Trim();

                preview.Rows.Add(previewRow);
            }
        }

        if (session.Records.Count == 0)
        {
            throw new InvalidDataException(
                "No usable telemetry rows were found. " +
                "Check the CSV headers and numeric values.");
        }

        return new TelemetryImportResult
        {
            Session = session,
            Preview = preview,
            ColumnCount = headers.Count,
            ValidRowCount = validRows,
            InvalidRowCount = invalidRows,
            MissingValueCount = missingValues,
            Delimiter = delimiter
        };
    }

    private static Dictionary<string, int> MapColumns(
        List<string> headers)
    {
        Dictionary<string, int> result =
            new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            string normalized = NormalizeHeader(headers[i]);

            string? channel = normalized switch
            {
                "time" or "timestamp" or "elapsedtime" or "sessiontime"
                    => "time",

                "speed" or "velocity" or "speedkmh" or "speedkph"
                    => "speed",

                "rpm" or "engine_rpm" or "enginerpm" or "revs"
                    => "rpm",

                "gear" or "gearposition"
                    => "gear",

                "throttle" or "throttleposition" or "accelerator"
                    => "throttle",

                "brake" or "brakepressure" or "brakeposition"
                    => "brake",

                "distance" or "distancem" or "lapdistance"
                    => "distance",

                "latitude" or "lat"
                    => "latitude",

                "longitude" or "lon" or "lng"
                    => "longitude",

                _ => null
            };

            if (channel is not null && !result.ContainsKey(channel))
                result.Add(channel, i);
        }

        return result;
    }

    private static string NormalizeHeader(string header)
    {
        StringBuilder result = new();

        foreach (char character in header.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
                result.Append(character);
        }

        return result.ToString();
    }

    private static bool TryParseNumber(string value, out double number)
    {
        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number) &&
            double.IsFinite(number))
        {
            return true;
        }

        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out number) &&
            double.IsFinite(number))
        {
            return true;
        }

        number = 0;
        return false;
    }

    private static Encoding DetectEncoding(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);

        byte[] prefix = new byte[4];
        int count = stream.Read(prefix, 0, prefix.Length);

        if (count >= 3 &&
            prefix[0] == 0xEF &&
            prefix[1] == 0xBB &&
            prefix[2] == 0xBF)
        {
            return new UTF8Encoding(true, true);
        }

        if (count >= 2 &&
            prefix[0] == 0xFF &&
            prefix[1] == 0xFE)
        {
            return new UnicodeEncoding(false, true, true);
        }

        if (count >= 2 &&
            prefix[0] == 0xFE &&
            prefix[1] == 0xFF)
        {
            return new UnicodeEncoding(true, true, true);
        }

        return new UTF8Encoding(false, true);
    }

    private static char DetectDelimiter(string header)
    {
        char[] candidates = [',', ';', '\t'];

        char best = ',';
        int bestCount = -1;

        foreach (char candidate in candidates)
        {
            int count = ParseCsvLine(header, candidate).Count;

            if (count > bestCount)
            {
                bestCount = count;
                best = candidate;
            }
        }

        return best;
    }

    private static List<string> ParseCsvLine(
        string line,
        char delimiter)
    {
        List<string> fields = [];
        StringBuilder field = new();
        bool insideQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];

            if (current == '"')
            {
                if (insideQuotes &&
                    i + 1 < line.Length &&
                    line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (current == delimiter && !insideQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(current);
            }
        }

        if (insideQuotes)
        {
            throw new InvalidDataException(
                "A CSV row contains an unclosed quoted field.");
        }

        fields.Add(field.ToString());
        return fields;
    }

    private static void MakeHeadersUnique(List<string> headers)
    {
        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            string original = headers[i];
            string candidate = original;
            int suffix = 2;

            while (!used.Add(candidate))
            {
                candidate = $"{original}_{suffix}";
                suffix++;
            }

            headers[i] = candidate;
        }
    }
}

public sealed class TelemetryImportResult
{
    public required TelemetrySession Session { get; init; }

    public required DataTable Preview { get; init; }

    public int ColumnCount { get; init; }

    public int ValidRowCount { get; init; }

    public int InvalidRowCount { get; init; }

    public int MissingValueCount { get; init; }

    public char Delimiter { get; init; }
}
