using System.Reflection;
using System.Text.RegularExpressions;

namespace NPUtil.Tests;

public class UnitTest1
{
    [Fact]
    public void AllXamlSymbols_MustBeValidFluentSymbols()
    {
        var asmPath = @"C:\Users\Asus\.nuget\packages\wpf-ui\4.3.0\lib\net9.0-windows7.0\Wpf.Ui.dll";
        var asm = Assembly.LoadFrom(asmPath);
        var symType = asm.GetType("Wpf.Ui.Controls.SymbolRegular");
        Assert.NotNull(symType);
        var validSymbols = new HashSet<string>(Enum.GetNames(symType));

        var xamlFiles = Directory.GetFiles(@"C:\Users\Asus\Downloads\NPUtil\src\NPUtil.App", "*.xaml", SearchOption.AllDirectories);
        var symbolRegex = new Regex(@"(?:Symbol=""(?<sym>[A-Za-z0-9_]+)""|\{ui:SymbolIcon\s+(?<sym>[A-Za-z0-9_]+)\})");

        var invalidSymbols = new List<string>();

        foreach (var file in xamlFiles)
        {
            var content = File.ReadAllText(file);
            var matches = symbolRegex.Matches(content);
            foreach (Match match in matches)
            {
                var sym = match.Groups["sym"].Value;
                if (!validSymbols.Contains(sym))
                {
                    invalidSymbols.Add($"{Path.GetFileName(file)}: '{sym}' is invalid");
                }
            }
        }

        Assert.True(invalidSymbols.Count == 0, "Invalid symbols found in XAML: " + string.Join("; ", invalidSymbols));
    }
}
