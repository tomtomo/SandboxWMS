using System.Text.Json;

namespace Wms.WebUI.Services;

// What: JsonSerializerOptions Web-defaults singleton untuk semua HttpClient JSON call.
// Why: ReadFromJsonAsync/JsonContent default CASE-SENSITIVE; API emit camelCase, DTO PascalCase →
// tanpa case-insensitive bisa deserialize silent-null. Web defaults = case-insensitive + camelCase.
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
