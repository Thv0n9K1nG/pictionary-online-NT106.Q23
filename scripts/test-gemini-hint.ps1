param(
    [string]$Word = 'bánh mì',
    [string]$KeyFile = '.\GameServer\secrets\gemini-api-key.txt',
    [string]$Model = ''
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedKeyFile = $null

if ([string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY)) {
    $candidateKeyFile = Join-Path $root $KeyFile
    if (Test-Path $candidateKeyFile) {
        $resolvedKeyFile = (Resolve-Path $candidateKeyFile).Path
        $env:GEMINI_API_KEY_FILE = $resolvedKeyFile
    }
}

if (-not [string]::IsNullOrWhiteSpace($Model)) {
    $env:GEMINI_MODEL = $Model
}

if ([string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY) -and
    [string]::IsNullOrWhiteSpace($env:GEMINI_API_KEY_FILE)) {
    Write-Host 'Missing Gemini API key.'
    Write-Host 'Set GEMINI_API_KEY, or create GameServer\secrets\gemini-api-key.txt from the example file.'
    exit 1
}

$tempRoot = Join-Path $env:TEMP ('pictionary-gemini-hint-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    Push-Location $tempRoot
    dotnet new console --framework net8.0 --force | Out-Null
    dotnet add reference (Join-Path $root 'GameServer\GameServer.csproj') | Out-Null

    $program = @'
using GameServer.Services;

var word = args.Length > 0 ? args[0] : "bánh mì";
var service = new GeminiService();

Console.WriteLine($"Gemini configured: {service.IsConfigured}");
Console.WriteLine($"Secret word: {word}");

var hint = await service.GenerateHintAsync(word);
Console.WriteLine($"Hint: {hint}");

if (string.IsNullOrWhiteSpace(hint))
{
    Console.Error.WriteLine("Hint is empty.");
    return 2;
}

var words = hint.Split(' ', StringSplitOptions.RemoveEmptyEntries);
if (hint.Length < 18 || words.Length < 4)
{
    Console.Error.WriteLine("Hint is too short to be useful.");
    return 3;
}

var normalizedHint = RemoveDiacritics(hint).ToLowerInvariant();
var normalizedWord = RemoveDiacritics(word).ToLowerInvariant();
if (normalizedHint.Contains(normalizedWord))
{
    Console.Error.WriteLine("Hint is too close: it contains the secret word.");
    return 4;
}

foreach (var token in normalizedWord.Split(' ', StringSplitOptions.RemoveEmptyEntries))
{
    if (token.Length >= 3 && normalizedHint.Contains(token))
    {
        Console.Error.WriteLine($"Hint is too close: it contains word token '{token}'.");
        return 5;
    }
}

return 0;

static string RemoveDiacritics(string value)
{
    var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
    var builder = new System.Text.StringBuilder(normalized.Length);
    foreach (var ch in normalized)
    {
        var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
        if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
        {
            builder.Append(ch);
        }
    }

    return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
}
'@

    Set-Content -Path (Join-Path $tempRoot 'Program.cs') -Value $program -Encoding UTF8
    $buildRoot = Join-Path $tempRoot ('build-' + [guid]::NewGuid().ToString('N'))
    $buildBase = $buildRoot + [System.IO.Path]::DirectorySeparatorChar
    dotnet run -p:BaseOutputPath=$buildBase -- $Word
}
finally {
    Pop-Location
    Write-Host "Temp test project: $tempRoot"
}
