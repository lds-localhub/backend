using System.Linq.Expressions;

using LocalHub.Domain.Locations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace LocalHub.Infrastructure.Locations;

// Maps GeoDistance.HaversineKm to SQL, so that the queries calculate the distance in PostgreSQL (Architecture
// documentation, DEC-06: Haversine formula in the database, without PostGIS or other extensions). It is the formula
// of the C# method, written with the functions of PostgreSQL, with φ = radians(latitude) and λ = radians(longitude):
//   12742.0 * asin(LEAST(1.0, sqrt(power(sin((φ2 - φ1) / 2.0), 2.0) + cos(φ1) * cos(φ2) * power(sin((λ2 - λ1) / 2.0), 2.0))))
// It is part of each query; no function is created in the database.
public static class GeoDistanceTranslation
{
    private static readonly DoubleTypeMapping DoublePrecision = new("double precision");

    public static void Register(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(GeoDistance).GetMethod(nameof(GeoDistance.HaversineKm))!)
            .HasTranslation(Translate);
    }

    // The arguments are latitude1, longitude1, latitude2 and longitude2, in degrees (numeric columns or parameters).
    private static SqlExpression Translate(IReadOnlyList<SqlExpression> arguments)
    {
        SqlFunctionExpression phi1 = Function("radians", arguments[0]);
        SqlFunctionExpression lambda1 = Function("radians", arguments[1]);
        SqlFunctionExpression phi2 = Function("radians", arguments[2]);
        SqlFunctionExpression lambda2 = Function("radians", arguments[3]);

        SqlBinaryExpression a = Binary(
            ExpressionType.Add,
            SquaredSineOfHalf(phi1, phi2),
            Binary(
                ExpressionType.Multiply,
                Binary(ExpressionType.Multiply, Function("cos", phi1), Function("cos", phi2)),
                SquaredSineOfHalf(lambda1, lambda2)));

        // LEAST(1.0, ...), like Math.Min in GeoDistance.HaversineKm. LEAST ignores NULL, so it does not propagate it: the
        // queries only pass users with a location.
        var atMostOne = new SqlFunctionExpression(
            "LEAST",
            [Constant(1), Function("sqrt", a)],
            nullable: true,
            argumentsPropagateNullability: [false, false],
            typeof(double),
            DoublePrecision);

        return Binary(ExpressionType.Multiply, Constant(2 * GeoDistance.EarthRadiusKm), Function("asin", atMostOne));
    }

    // power(sin((to - from) / 2.0), 2.0)
    private static SqlFunctionExpression SquaredSineOfHalf(SqlExpression from, SqlExpression to)
    {
        SqlBinaryExpression half = Binary(ExpressionType.Divide, Binary(ExpressionType.Subtract, to, from), Constant(2));
        return Function("power", Function("sin", half), Constant(2));
    }

    // A PostgreSQL function that returns NULL when its argument is NULL.
    private static SqlFunctionExpression Function(string name, params SqlExpression[] arguments) =>
        new(name, arguments, nullable: true, argumentsPropagateNullability: arguments.Select(_ => true), typeof(double),
            DoublePrecision);

    private static SqlBinaryExpression Binary(ExpressionType operation, SqlExpression left, SqlExpression right) =>
        new(operation, left, right, typeof(double), DoublePrecision);

    private static SqlConstantExpression Constant(double value) => new(value, typeof(double), DoublePrecision);
}