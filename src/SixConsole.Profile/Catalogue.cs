using System.IO;
using System.Reflection;
using System.Text.Json;

namespace SixConsole.Profile;

/// <summary>
/// Loads the operator/weapon catalogue (generated once from the Lua source by
/// tools/generate-catalogue.py and committed as Data/operators.json);
/// the JSON is embedded in this assembly. Auxiliary runs can load overrides via file.
/// </summary>
public static class Catalogue
{
    private static readonly Lazy<IReadOnlyList<Operator>> LazyOperators = new(LoadEmbedded);

    private static IReadOnlyList<Operator> LoadEmbedded()
    {
        var assembly = typeof(Catalogue).Assembly;
        var resourceName = "SixConsole.Profile.Data.operators.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Parse(ms.ToArray());
    }

    public static IReadOnlyList<Operator> Parse(byte[] jsonBytes)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip };
        var doc = JsonSerializer.Deserialize<CatalogueDoc>(jsonBytes, options)
            ?? throw new InvalidOperationException("Catalogue JSON deserialized to null");
        var operators = new List<Operator>();
        foreach (var o in doc.Operators)
        {
            operators.Add(new Operator
            {
                Id = o.Id,
                Name = o.Name,
                Side = OperatorSideExtensions.FromLabel(o.Side),
                Weapons = o.Weapons.Select(w => new Weapon { Id = w.Id, Name = w.Name, Pull = w.Pull, RapidFire = w.RapidFire }).ToList(),
            });
        }
        if (operators.Count == 0) throw new InvalidOperationException("Catalogue is empty");
        return operators;
    }

    public static IReadOnlyList<Operator> Operators => LazyOperators.Value;


    public static IEnumerable<Operator> BySide(OperatorSide side) => Operators.Where(o => o.Side == side);

    private sealed record CatalogueDoc(IReadOnlyList<CatalogueOperator> Operators);
    private sealed record CatalogueOperator(string Name, string Side, string Id, IReadOnlyList<CatalogueWeapon> Weapons);
    private sealed record CatalogueWeapon(string Id, string Name, int Pull, bool RapidFire);
}
