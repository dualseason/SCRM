using System.Text;

namespace SCRM.CodeGenerator
{
    /// <summary>
    /// 通用代码生成器，生成Entity、DTO、Repository和Controller
    /// </summary>
    public class CodeGenerator
    {
        private readonly List<TableDefinition> _tables;

        public CodeGenerator(string sqlFilePath)
        {
            _tables = TableAnalyzer.ParseSqlScript(sqlFilePath);
        }

        public void GenerateAllFiles(string baseOutputPath)
        {
            foreach (var table in _tables)
            {
                // Generate Entity Model
                var entityCode = GenerateEntityModel(table);
                SaveFile(Path.Combine(baseOutputPath, "Models", "Entities", $"{TableAnalyzer.ToPascalCase(table.TableName)}.cs"), entityCode);

                // Generate DTO
                var dtoCode = GenerateDtoModel(table);
                SaveFile(Path.Combine(baseOutputPath, "Models", "Dtos", $"{TableAnalyzer.ToPascalCase(table.TableName)}Dto.cs"), dtoCode);

                // Generate Repository Interface
                var repositoryInterfaceCode = GenerateRepositoryInterface(table);
                SaveFile(Path.Combine(baseOutputPath, "Services", "Repository", $"I{TableAnalyzer.ToPascalCase(table.TableName)}Repository.cs"), repositoryInterfaceCode);

                // Generate Repository Implementation
                var repositoryCode = GenerateRepository(table);
                SaveFile(Path.Combine(baseOutputPath, "Services", "Repository", $"{TableAnalyzer.ToPascalCase(table.TableName)}Repository.cs"), repositoryCode);

                // Generate Controller
                var controllerCode = GenerateController(table);
                SaveFile(Path.Combine(baseOutputPath, "Controllers", $"{TableAnalyzer.ToPascalCase(table.TableName)}Controller.cs"), controllerCode);
            }
        }

        private string GenerateEntityModel(TableDefinition table)
        {
            var className = TableAnalyzer.ToPascalCase(table.TableName);
            var sb = new StringBuilder();

            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            sb.AppendLine();
            sb.AppendLine("namespace SCRM.Models.Entities");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {table.Comment}");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    [Table(\"{table.TableName}\")]");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");

            foreach (var col in table.Columns)
            {
                var propName = TableAnalyzer.ToPascalCase(col.ColumnName);
                var propType = TableAnalyzer.ConvertCSharpType(col.DataType);
                var nullable = col.IsNullable && propType != "string" && propType != "object" ? "?" : "";

                if (col.IsPrimaryKey)
                    sb.AppendLine($"        [Key]");

                if (!col.IsNullable && propType != "string")
                    sb.AppendLine($"        [Required]");

                if (col.ColumnName != propName)
                    sb.AppendLine($"        [Column(\"{col.ColumnName}\")]");

                sb.AppendLine($"        /// <summary>{col.Comment}</summary>");
                sb.AppendLine($"        public {propType}{nullable} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateDtoModel(TableDefinition table)
        {
            var className = TableAnalyzer.ToPascalCase(table.TableName);
            var sb = new StringBuilder();

            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            sb.AppendLine();
            sb.AppendLine("namespace SCRM.Models.Dtos");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {table.Comment} DTO");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className}Dto");
            sb.AppendLine("    {");

            foreach (var col in table.Columns)
            {
                var propName = TableAnalyzer.ToPascalCase(col.ColumnName);
                var propType = TableAnalyzer.ConvertCSharpType(col.DataType);
                var nullable = col.IsNullable && propType != "string" && propType != "object" ? "?" : "";

                if (!col.IsNullable && propType != "string" && !col.IsAutoIncrement)
                    sb.AppendLine($"        [Required]");

                sb.AppendLine($"        /// <summary>{col.Comment}</summary>");
                sb.AppendLine($"        public {propType}{nullable} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateRepositoryInterface(TableDefinition table)
        {
            var className = TableAnalyzer.ToPascalCase(table.TableName);
            var primaryKeyCol = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
            var keyType = primaryKeyCol != null ? TableAnalyzer.ConvertCSharpType(primaryKeyCol.DataType) : "long";

            var sb = new StringBuilder();
            sb.AppendLine("using SCRM.Core.Repository;");
            sb.AppendLine("using SCRM.Models.Entities;");
            sb.AppendLine();
            sb.AppendLine("namespace SCRM.Services.Repository");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {className} 仓储接口");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public interface I{className}Repository : IBaseRepository<{className}, {keyType}>");
            sb.AppendLine("    {");
            sb.AppendLine("        // 在此添加特定的业务方法");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateRepository(TableDefinition table)
        {
            var className = TableAnalyzer.ToPascalCase(table.TableName);
            var primaryKeyCol = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
            var keyType = primaryKeyCol != null ? TableAnalyzer.ConvertCSharpType(primaryKeyCol.DataType) : "long";

            var sb = new StringBuilder();
            sb.AppendLine("using SCRM.Core.Repository;");
            sb.AppendLine("using SCRM.Models.Entities;");
            sb.AppendLine("using SCRM.Services.Data;");
            sb.AppendLine();
            sb.AppendLine("namespace SCRM.Services.Repository");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {className} 仓储实现");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className}Repository : BaseRepository<{className}, {keyType}>, I{className}Repository");
            sb.AppendLine("    {");
            sb.AppendLine("        public {className}Repository(ApplicationDbContext context) : base(context)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        // 在此添加特定的业务逻辑实现");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateController(TableDefinition table)
        {
            var className = TableAnalyzer.ToPascalCase(table.TableName);
            var lowerName = TableAnalyzer.ToCamelCase(table.TableName);
            var primaryKeyCol = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
            var primaryKeyName = primaryKeyCol != null ? TableAnalyzer.ToPascalCase(primaryKeyCol.ColumnName) : "Id";
            var keyType = primaryKeyCol != null ? TableAnalyzer.ConvertCSharpType(primaryKeyCol.DataType) : "long";

            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.AspNetCore.Authorization;");
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine("using Swashbuckle.AspNetCore.Annotations;");
            sb.AppendLine("using SCRM.Core.Controllers;");
            sb.AppendLine("using SCRM.Models.Dtos;");
            sb.AppendLine("using SCRM.Models.Entities;");
            sb.AppendLine("using SCRM.Services.Repository;");
            sb.AppendLine();
            sb.AppendLine("namespace SCRM.Controllers");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {className} 控制器");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    [Authorize]");
            sb.AppendLine($"    [ApiController]");
            sb.AppendLine($"    [Route(\"api/[controller]\")]");
            sb.AppendLine($"    [SwaggerTag(\"{table.Comment}\")]");
            sb.AppendLine($"    public class {className}Controller : BaseApiController<{className}Dto>");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly I{className}Repository _{lowerName}Repository;");
            sb.AppendLine();
            sb.AppendLine($"        public {className}Controller(I{className}Repository {lowerName}Repository)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _{lowerName}Repository = {lowerName}Repository;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GET all
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// 获取所有{table.Comment}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        [HttpGet]");
            sb.AppendLine($"        [SwaggerOperation(Summary = \"获取所有{table.Comment}\")]");
            sb.AppendLine($"        public async Task<IActionResult> GetAll()");
            sb.AppendLine("        {");
            sb.AppendLine($"            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var items = await _{lowerName}Repository.GetAllAsync();");
            sb.AppendLine($"                return OkResponse(items.Select(MapToDto).ToList(), \"获取成功\");");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return ErrorResponse<List<{className}Dto>>(ex.Message);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GET by ID
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// 根据ID获取{table.Comment}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        [HttpGet(\"{{id}}\")]");
            sb.AppendLine($"        [SwaggerOperation(Summary = \"根据ID获取{table.Comment}\")]");
            sb.AppendLine($"        public async Task<IActionResult> GetById({keyType} id)");
            sb.AppendLine("        {");
            sb.AppendLine($"            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var item = await _{lowerName}Repository.GetByIdAsync(id);");
            sb.AppendLine($"                if (item == null)");
            sb.AppendLine($"                    return NotFoundResponse<{className}Dto>(\"记录不存在\");");
            sb.AppendLine();
            sb.AppendLine($"                return OkResponse(MapToDto(item), \"获取成功\");");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return ErrorResponse<{className}Dto>(ex.Message);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // POST create
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// 创建新的{table.Comment}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("[HttpPost]");
            sb.AppendLine($"        [SwaggerOperation(Summary = \"创建新的{table.Comment}\")]");
            sb.AppendLine($"        public async Task<IActionResult> Create([FromBody] {className}Dto dto)");
            sb.AppendLine("        {");
            sb.AppendLine($"            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                if (!ModelState.IsValid)");
            sb.AppendLine($"                    return BadRequestResponse<{className}Dto>(\"输入验证失败\");");
            sb.AppendLine();
            sb.AppendLine($"                var entity = MapToEntity(dto);");
            sb.AppendLine($"                var result = await _{lowerName}Repository.AddAsync(entity);");
            sb.AppendLine();
            sb.AppendLine($"                return Ok(new {{ Success = true, Data = MapToDto(result), Message = \"创建成功\" }});");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return ErrorResponse<{className}Dto>(ex.Message);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // PUT update
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// 更新{table.Comment}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        [HttpPut(\"{{id}}\")]");
            sb.AppendLine($"        [SwaggerOperation(Summary = \"更新{table.Comment}\")]");
            sb.AppendLine($"        public async Task<IActionResult> Update({keyType} id, [FromBody] {className}Dto dto)");
            sb.AppendLine("        {");
            sb.AppendLine($"            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                if (!ModelState.IsValid)");
            sb.AppendLine($"                    return BadRequestResponse<{className}Dto>(\"输入验证失败\");");
            sb.AppendLine();
            sb.AppendLine($"                var entity = await _{lowerName}Repository.GetByIdAsync(id);");
            sb.AppendLine($"                if (entity == null)");
            sb.AppendLine($"                    return NotFoundResponse<{className}Dto>(\"记录不存在\");");
            sb.AppendLine();
            sb.AppendLine($"                MapToEntity(dto, entity);");
            sb.AppendLine($"                var result = await _{lowerName}Repository.UpdateAsync(entity);");
            sb.AppendLine();
            sb.AppendLine($"                return OkResponse(MapToDto(result), \"更新成功\");");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return ErrorResponse<{className}Dto>(ex.Message);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // DELETE
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// 删除{table.Comment}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        [HttpDelete(\"{{id}}\")]");
            sb.AppendLine($"        [SwaggerOperation(Summary = \"删除{table.Comment}\")]");
            sb.AppendLine($"        public async Task<IActionResult> Delete({keyType} id)");
            sb.AppendLine("        {");
            sb.AppendLine($"            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var result = await _{lowerName}Repository.DeleteAsync(id);");
            sb.AppendLine($"                if (!result)");
            sb.AppendLine($"                    return NotFoundResponse<object>(\"记录不存在\");");
            sb.AppendLine();
            sb.AppendLine($"                return OkResponse<object>(null, \"删除成功\");");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return ErrorResponse<object>(ex.Message);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GET paged
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// 分页获取{table.Comment}");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("[HttpGet(\"page\")]");
            sb.AppendLine($"        [SwaggerOperation(Summary = \"分页获取{table.Comment}\")]");
            sb.AppendLine($"        public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)");
            sb.AppendLine("        {");
            sb.AppendLine($"            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var (items, total) = await _{lowerName}Repository.GetPagedAsync(pageNumber, pageSize);");
            sb.AppendLine($"                return OkPagedResponse(pageNumber, pageSize, total, items.Select(MapToDto).ToList());");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return ErrorResponse<PagedResponse<{className}Dto>>(ex.Message);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Mapping methods
            sb.AppendLine("        private {className}Dto MapToDto({className} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {className}Dto");
            sb.AppendLine("            {");
            foreach (var col in table.Columns)
            {
                var propName = TableAnalyzer.ToPascalCase(col.ColumnName);
                sb.AppendLine($"                {propName} = entity.{propName},");
            }
            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        private {className} MapToEntity({className}Dto dto, {className}? entity = null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            entity ??= new {className}();");
            foreach (var col in table.Columns)
            {
                if (col.IsAutoIncrement) continue;
                var propName = TableAnalyzer.ToPascalCase(col.ColumnName);
                sb.AppendLine($"            entity.{propName} = dto.{propName};");
            }
            sb.AppendLine($"            return entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void SaveFile(string filePath, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content, Encoding.UTF8);
            Console.WriteLine($"Generated: {filePath}");
        }
    }
}
