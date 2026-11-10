using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Materijalno.Import
{
    public sealed class DbfField
    {
        public string Name { get; set; }
        public char Type { get; set; }
        public int Length { get; set; }
        public int Decimals { get; set; }
        public int Flags { get; set; }
    }

    public sealed class DbfRecord
    {
        public int RecordNumber { get; set; }
        public bool Deleted { get; set; }
        public Dictionary<string, string> Values { get; set; }
        public byte[] RawBytes { get; set; }
    }

    public sealed class DbfTable
    {
        public List<DbfField> Fields { get; set; }
        public List<DbfRecord> Records { get; set; }
    }

    // Reads the original file only. Deleted records are retained in the raw archive.
    public static class DbfReader
    {
        public static DbfTable Read(string fileName)
        {
            using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file, Encoding.GetEncoding(1250)))
            {
                var header = reader.ReadBytes(32);
                if (header.Length != 32) throw new InvalidDataException("Nepotpuno DBF zaglavlje.");
                int count = BitConverter.ToInt32(header, 4);
                int headerSize = BitConverter.ToUInt16(header, 8);
                int recordSize = BitConverter.ToUInt16(header, 10);
                if (count < 0 || headerSize < 33 || recordSize < 1)
                    throw new InvalidDataException("Neispravna DBF struktura.");
                var result = new DbfTable { Fields = new List<DbfField>(), Records = new List<DbfRecord>() };
                while (file.Position < headerSize)
                {
                    byte first = reader.ReadByte();
                    if (first == 13) break;
                    var descriptor = new byte[32];
                    descriptor[0] = first;
                    var rest = reader.ReadBytes(31);
                    if (rest.Length != 31) throw new InvalidDataException("Nepotpuna definicija polja.");
                    Array.Copy(rest, 0, descriptor, 1, 31);
                    string name = Encoding.ASCII.GetString(descriptor, 0, 11).TrimEnd('\0');
                    char type = (char)descriptor[11];
                    if ("CNFDILYMT".IndexOf(type) < 0)
                        throw new NotSupportedException("DBF tip " + type + " nije podržan za " + name + ".");
                    result.Fields.Add(new DbfField { Name = name, Type = type, Length = descriptor[16], Decimals = descriptor[17], Flags = descriptor[18] });
                }
                file.Position = headerSize;
                var encoding = Encoding.GetEncoding(1250);
                byte[] memo = null;
                int memoBlockSize = 0;
                if (result.Fields.Exists(f => f.Type == 'M'))
                {
                    string memoPath = Path.ChangeExtension(fileName, ".fpt");
                    memo = File.ReadAllBytes(memoPath);
                    if (memo.Length < 8) throw new InvalidDataException("Nepotpuno FPT zaglavlje.");
                    memoBlockSize = (memo[6] << 8) | memo[7];
                    if (memoBlockSize <= 0) throw new InvalidDataException("Neispravna veličina FPT bloka.");
                }
                for (int i = 0; i < count; i++)
                {
                    byte[] data = reader.ReadBytes(recordSize);
                    if (data.Length != recordSize) throw new InvalidDataException("Nepotpun DBF red " + (i + 1));
                    var row = new DbfRecord { RecordNumber = i + 1, Deleted = data[0] == 42, RawBytes = data,
                        Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) };
                    int offset = 1;
                    foreach (var field in result.Fields)
                    {
                        if (offset + field.Length > data.Length) throw new InvalidDataException("Neispravna dužina DBF reda.");
                        string value;
                        if (field.Type == 'I') value = BitConverter.ToInt32(data, offset).ToString(CultureInfo.InvariantCulture);
                        else if (field.Type == 'Y') value = (BitConverter.ToInt64(data, offset) / 10000m).ToString(CultureInfo.InvariantCulture);
                        else if (field.Type == 'T')
                        {
                            int julian = BitConverter.ToInt32(data, offset), millis = BitConverter.ToInt32(data, offset + 4);
                            value = julian == 0 && millis == 0 ? "" : new DateTime(1, 1, 1).AddDays(julian - 1721426L).AddMilliseconds(millis).ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                        }
                        else if (field.Type == 'M')
                        {
                            int pointer = field.Length == 4 ? BitConverter.ToInt32(data, offset) : int.Parse(encoding.GetString(data, offset, field.Length).Trim(), CultureInfo.InvariantCulture);
                            if (pointer == 0) value = "";
                            else
                            {
                                long start = (long)pointer * memoBlockSize;
                                if (start < 0 || start + 8 > memo.Length) throw new InvalidDataException("Neispravan FPT pokazivač.");
                                int p = checked((int)start);
                                int kind = (memo[p] << 24) | (memo[p + 1] << 16) | (memo[p + 2] << 8) | memo[p + 3];
                                int length = (memo[p + 4] << 24) | (memo[p + 5] << 16) | (memo[p + 6] << 8) | memo[p + 7];
                                if (length < 0 || start + 8 + length > memo.Length) throw new InvalidDataException("Nepotpun FPT sadržaj.");
                                value = kind == 1 && (field.Flags & 4) == 0 ? encoding.GetString(memo, p + 8, length) : "base64:" + Convert.ToBase64String(memo, p + 8, length);
                            }
                        }
                        else value = encoding.GetString(data, offset, field.Length).TrimEnd(' ', '\0');
                        row.Values.Add(field.Name, value);
                        offset += field.Length;
                    }
                    result.Records.Add(row);
                }
                return result;
            }
        }
    }
}
