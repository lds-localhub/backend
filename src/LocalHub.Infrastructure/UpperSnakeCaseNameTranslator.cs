using Npgsql;
using Npgsql.NameTranslation;

namespace LocalHub.Infrastructure;

public sealed class UpperSnakeCaseNameTranslator : INpgsqlNameTranslator
{
    public static readonly UpperSnakeCaseNameTranslator Instance = new UpperSnakeCaseNameTranslator();
    private static readonly NpgsqlSnakeCaseNameTranslator Snake = new NpgsqlSnakeCaseNameTranslator();

    public string TranslateTypeName(string clrName)
    {
        return Snake.TranslateTypeName(clrName);
    }

    public string TranslateMemberName(string clrName)
    {
        return Snake.TranslateMemberName(clrName).ToUpperInvariant();
    }
}