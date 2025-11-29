using System.Text.RegularExpressions;

namespace SCRM.CodeGenerator
{
    /// <summary>
    /// SQL脚本分析器，用于从Script.sql提取表结构
    /// </summary>
    public class TableDefinition
    {
        public string TableName { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public List<ColumnDefinition> Columns { get; set; } = new();
    }

    public class ColumnDefinition
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; } = true;
        public bool IsPrimaryKey { get; set; }
        public string? DefaultValue { get; set; }
        public string Comment { get; set; } = string.Empty;
        public bool IsAutoIncrement { get; set; }
    }

    public class TableAnalyzer
    {
        public static List<TableDefinition> ParseSqlScript(string filePath)
        {
            var tables = new List<TableDefinition>();
            var content = File.ReadAllText(filePath);

            // Extract all CREATE TABLE statements
            var tablePattern = @"CREATE TABLE[""]*(\w+)[\""]*\s*\(([\s\S]*?)\);";
            var matches = Regex.Matches(content, tablePattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var tableName = match.Groups[1].Value;
                var columns = ParseColumns(match.Groups[2].Value);
                var comment = ExtractTableComment(content, tableName);

                tables.Add(new TableDefinition
                {
                    TableName = tableName,
                    Comment = comment,
                    Columns = columns
                });
            }

            return tables;
        }

        private static List<ColumnDefinition> ParseColumns(string columnsText)
        {
            var columns = new List<ColumnDefinition>();
            var lines = columnsText.Split(',');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("CONSTRAINT") || trimmed.StartsWith("UNIQUE"))
                    continue;

                var parts = Regex.Split(trimmed, @"\s+");
                if (parts.Length < 2)
                    continue;

                var columnName = parts[0].Trim('"', '[', ']');
                var dataType = parts[1];
                var isNullable = !trimmed.Contains("NOT NULL");
                var isPrimaryKey = trimmed.Contains("PRIMARY KEY");
                var isAutoIncrement = dataType.Contains("bigserial") || dataType.Contains("serial");

                columns.Add(new ColumnDefinition
                {
                    ColumnName = columnName,
                    DataType = dataType,
                    IsNullable = isNullable,
                    IsPrimaryKey = isPrimaryKey,
                    IsAutoIncrement = isAutoIncrement
                });
            }

            return columns;
        }

        private static string ExtractTableComment(string content, string tableName)
        {
            var commentPattern = $@"COMMENT ON TABLE[""]*{tableName}[\""]*\s+IS\s+'([^']+)'";
            var match = Regex.Match(content, commentPattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public static string ConvertCSharpType(string sqlType)
        {
            return sqlType.ToLower() switch
            {
                "bigserial" => "long",
                "int8" => "long",
                "int4" or "serial" => "int",
                "int2" => "short",
                "varchar" or "text" => "string",
                "bool" => "bool",
                "timestamp" => "DateTime",
                "timestamptz" => "DateTime",
                "float8" or "double precision" => "double",
                "float4" or "real" => "float",
                "numeric" or "decimal" => "decimal",
                "uuid" => "Guid",
                "jsonb" or "json" => "string",
                _ => "object"
            };
        }

        public static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var words = input.Split('_');
            return string.Concat(words.Select(w =>
                char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "")
            ));
        }

        public static string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var pascal = ToPascalCase(input);
            return char.ToLower(pascal[0]) + pascal[1..];
        }
    }
}
