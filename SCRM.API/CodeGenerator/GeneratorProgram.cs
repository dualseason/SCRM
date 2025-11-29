using SCRM.CodeGenerator;

// Path to SQL script
var sqlFilePath = Path.Combine(Directory.GetCurrentDirectory(), "DB", "Script.sql");
var baseOutputPath = Directory.GetCurrentDirectory();

if (!File.Exists(sqlFilePath))
{
    Console.WriteLine($"SQL文件未找到: {sqlFilePath}");
    return;
}

Console.WriteLine("开始生成代码...");
Console.WriteLine($"SQL文件: {sqlFilePath}");
Console.WriteLine($"输出路径: {baseOutputPath}");
Console.WriteLine();

try
{
    var generator = new CodeGenerator(sqlFilePath);
    generator.GenerateAllFiles(baseOutputPath);
    Console.WriteLine();
    Console.WriteLine("代码生成完成！");
}
catch (Exception ex)
{
    Console.WriteLine($"生成失败: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
