using System.Linq.Expressions;

namespace GroundUp.Data.Core.Utilities;

public static class ExpressionHelper
{
    public static IQueryable<T> ApplySorting<T>(IQueryable<T> query, string? sortBy)
    {
        // Preserve behavior: if not provided, do no ordering.
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return query;
        }

        // Simple parsing: "Field" or "-Field" for descending.
        var descending = sortBy.StartsWith('-');
        var propertyName = descending ? sortBy[1..] : sortBy;

        var property = typeof(T).GetProperties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        if (property == null)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(parameter, property);
        var lambda = Expression.Lambda(member, parameter);

        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2);

        var generic = method.MakeGenericMethod(typeof(T), property.PropertyType);
        return (IQueryable<T>)generic.Invoke(null, new object[] { query, lambda })!;
    }

    public static Expression<Func<T, bool>> BuildPredicate<T>(System.Reflection.PropertyInfo property, string value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(parameter, property);

        var typedValue = ConvertTo(value, property.PropertyType);
        var constant = Expression.Constant(typedValue, property.PropertyType);

        var body = Expression.Equal(member, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static Expression<Func<T, bool>> BuildContainsPredicate<T>(System.Reflection.PropertyInfo property, string value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(parameter, property);

        // string.Contains
        if (property.PropertyType != typeof(string))
        {
            // fallback to equality for non-string
            return BuildPredicate<T>(property, value);
        }

        var constant = Expression.Constant(value, typeof(string));
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        var body = Expression.Call(member, containsMethod, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static Expression<Func<T, bool>> BuildRangePredicate<T>(System.Reflection.PropertyInfo property, string value, bool isMin)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(parameter, property);

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var typedValue = ConvertTo(value, targetType);

        var constant = Expression.Constant(typedValue, targetType);
        Expression left = member;
        if (member.Type != targetType)
        {
            left = Expression.Convert(member, targetType);
        }

        var body = isMin ? Expression.GreaterThanOrEqual(left, constant) : Expression.LessThanOrEqual(left, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static Expression<Func<T, bool>> BuildDateRangePredicate<T>(System.Reflection.PropertyInfo property, string value, bool isMin)
    {
        return BuildRangePredicate<T>(property, value, isMin);
    }

    private static object? ConvertTo(string value, Type targetType)
    {
        if (targetType == typeof(string)) return value;
        if (targetType == typeof(Guid)) return Guid.Parse(value);
        if (targetType.IsEnum) return Enum.Parse(targetType, value, ignoreCase: true);

        return Convert.ChangeType(value, targetType);
    }
}
