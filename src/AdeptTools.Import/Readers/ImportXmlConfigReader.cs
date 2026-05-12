using System.Xml;
using System.Xml.Schema;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;

namespace AdeptTools.Import.Readers;

public class ImportXmlConfigReader
{
    public (ImportConfig Config, List<ColumnMapping> Mappings) ReadConfig(string xmlPath)
    {
        var config = new ImportConfig();
        var mappings = new List<ColumnMapping>();

        var doc = new XmlDocument();
        doc.Load(xmlPath);

        var root = doc.DocumentElement;
        if (root is null)
            throw new InvalidOperationException("XML config file is empty.");

        // Read settings
        config.ServerUrl = GetChildText(root, "ServerUrl");
        config.ImportMode = ParseImportMode(GetChildText(root, "ImportMode"));
        config.AddIfNotFound = ParseBool(GetChildText(root, "AddIfNotFound"));
        config.WorkAreaId = GetChildText(root, "WorkAreaId");
        config.SkipHiddenRows = ParseBool(GetChildText(root, "SkipHiddenRows"));
        config.DryRun = ParseBool(GetChildText(root, "DryRun"));

        var headerRowsText = GetChildText(root, "HeaderRows");
        if (int.TryParse(headerRowsText, out var headerRows) && headerRows > 0)
            config.HeaderRows = headerRows;

        // Read mappings
        var mappingsNode = root.SelectSingleNode("Mappings");
        if (mappingsNode is not null)
        {
            foreach (XmlNode mappingNode in mappingsNode.SelectNodes("Mapping")!)
            {
                if (mappingNode is not XmlElement elem) continue;

                var excelColumn = elem.GetAttribute("ExcelColumn");
                var adeptField = elem.GetAttribute("AdeptField");
                var actionText = elem.GetAttribute("Action");

                if (string.IsNullOrWhiteSpace(excelColumn) || string.IsNullOrWhiteSpace(adeptField))
                    continue;

                var action = ParseMappingAction(actionText);
                if (action == MappingAction.DoNotImport)
                    continue;

                var mapping = new ColumnMapping
                {
                    ExcelColumn = excelColumn,
                    AdeptField = adeptField,
                    Action = action
                };

                var operatorText = elem.GetAttribute("Operator");
                if (!string.IsNullOrWhiteSpace(operatorText))
                    mapping.Operator = ParseSearchOperator(operatorText);

                var dateRangeColumn = elem.GetAttribute("DateRangeColumn");
                if (!string.IsNullOrWhiteSpace(dateRangeColumn))
                    mapping.DateRangeColumn = dateRangeColumn;

                mappings.Add(mapping);
            }
        }

        return (config, mappings);
    }

    private static string GetChildText(XmlElement parent, string childName)
    {
        var node = parent.SelectSingleNode(childName);
        return node?.InnerText?.Trim() ?? string.Empty;
    }

    private static bool ParseBool(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "YES" or "1" => true,
            _ => false
        };
    }

    private static ImportMode ParseImportMode(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ImportMode.UpdateDataCard;
        return text.Trim().ToLowerInvariant() switch
        {
            "searchresultsonly" or "search" => ImportMode.SearchResultsOnly,
            _ => ImportMode.UpdateDataCard
        };
    }

    private static MappingAction ParseMappingAction(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return MappingAction.DoNotImport;
        return text.Trim().ToLowerInvariant() switch
        {
            "searchkey" or "search" => MappingAction.SearchKey,
            "fillifempty" or "ifempty" => MappingAction.FillIfEmpty,
            "filloverwrite" or "overwrite" or "fill" => MappingAction.FillOverwrite,
            _ => MappingAction.DoNotImport
        };
    }

    private static SearchOperator? ParseSearchOperator(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return SearchOperator.Equals;
        return text.Trim().ToLowerInvariant() switch
        {
            "equals" or "eq" => SearchOperator.Equals,
            "dateafter" or "after" => SearchOperator.DateAfter,
            "datebefore" or "before" => SearchOperator.DateBefore,
            "datebetween" or "between" => SearchOperator.DateBetween,
            _ => SearchOperator.Equals
        };
    }
}
