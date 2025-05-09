using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Fortnite;
using Helios.Utilities;
using Microsoft.VisualBasic.FileIO;

namespace Helios.Services
{
    public class CloudStorageImportService
    {
        private readonly Func<Repository<CloudStorage>> _repository = Constants.repositoryPool.Repo<CloudStorage>();
        
        /// <summary>
        /// Imports CSV data into the CloudStorage table if the table is empty
        /// </summary>
        /// <param name="csvFilePath">Path to the CSV file containing the CloudStorage data</param>
        /// <returns>Number of records imported</returns>
        public async Task<int> ImportFromCsvIfEmptyAsync(string csvFilePath)
        {
            var template = new CloudStorage();
            var existingData = await _repository().FindManyAsync(template, 1);

            if (existingData.Count > 0)
                return 0;
            
            var records = ParseCsvFile(csvFilePath);

            if (records.Count == 0)
                return 0;
            
            await _repository().BulkInsertAsync(records);
            Logger.Info($"Successfully imported {records.Count} CloudStorage records.");
            return records.Count;
        }
        
        private List<CloudStorage> ParseCsvFile(string filePath)
        {
            var records = new List<CloudStorage>();

            using (var parser = new TextFieldParser(filePath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.Delimiters = new[] { "," };
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = true;

                if (!parser.EndOfData)
                    parser.ReadFields();

                while (!parser.EndOfData)
                {
                    string[] fields;
                    try
                    {
                        fields = parser.ReadFields();
                    }
                    catch (MalformedLineException ex)
                    {
                        Console.WriteLine($"Malformed line skipped: {ex.Message}");
                        continue;
                    }

                    if (fields == null || fields.Length < 3)
                    {
                        Console.WriteLine("Skipping line with insufficient fields.");
                        continue;
                    }

                    string filename = fields[0];
                    string value = fields[1];
                    string isEnabledStr = fields[2];

                    if (!bool.TryParse(isEnabledStr, out bool isEnabled))
                    {
                        Console.WriteLine($"Invalid boolean value '{isEnabledStr}' in line: {string.Join(",", fields)}");
                        continue;
                    }

                    records.Add(new CloudStorage
                    {
                        Filename = filename,
                        Value = value,
                        Enabled = isEnabled
                    });
                }
            }

            return records;
        }
    }
}