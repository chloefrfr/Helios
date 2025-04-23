using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly Repository<CloudStorage> _repository = Constants.repositoryPool.GetRepository<CloudStorage>();
        
        public async Task<int> ImportOrUpdateFromCsvAsync(string csvFilePath)
        {
            var newRecords = ParseCsvFile(csvFilePath);
            if (newRecords.Count == 0)
            {
                Logger.Info("No records found in CSV file.");
                return 0;
            }

            var existingRecords = await _repository.FindManyAsync(new CloudStorage());
            var existingDict = existingRecords.ToDictionary(r => r.Filename);

            var inserts = new List<CloudStorage>();
            var updates = new List<CloudStorage>();

            foreach (var newRecord in newRecords)
            {
                if (existingDict.TryGetValue(newRecord.Filename, out var existingRecord))
                {
                    if (existingRecord.Value != newRecord.Value || 
                        existingRecord.IsEnabled != newRecord.IsEnabled)
                    {
                        existingRecord.Value = newRecord.Value;
                        existingRecord.IsEnabled = newRecord.IsEnabled;
                        updates.Add(existingRecord);
                    }
                }
                else
                {
                    inserts.Add(newRecord);
                }
            }

            int affectedRows = 0;
            if (inserts.Count > 0)
            {
                await _repository.BulkInsertAsync(inserts);
                affectedRows += inserts.Count;
                Logger.Info($"Inserted {inserts.Count} new records.");
            }

            if (updates.Count > 0)
            {
                await _repository.BulkUpdateAsync(updates); 
                affectedRows += updates.Count;
                Logger.Info($"Updated {updates.Count} existing records.");
            }

            if (affectedRows == 0)
                Logger.Info("Database is already up-to-date with the CSV.");

            return affectedRows;
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
                        Logger.Error($"Malformed line skipped: {ex.Message}");
                        continue;
                    }

                    if (fields == null || fields.Length < 3)
                    {
                        Logger.Warn("Skipping line with insufficient fields.");
                        continue;
                    }

                    string filename = fields[0];
                    string value = fields[1];
                    string isEnabledStr = fields[2];

                    if (!bool.TryParse(isEnabledStr, out bool isEnabled))
                    {
                        Logger.Warn($"Invalid boolean value '{isEnabledStr}' in line: {filename}");
                        continue;
                    }

                    records.Add(new CloudStorage
                    {
                        Filename = filename,
                        Value = value,
                        IsEnabled = isEnabled
                    });
                }
            }
            return records;
        }
    }
}