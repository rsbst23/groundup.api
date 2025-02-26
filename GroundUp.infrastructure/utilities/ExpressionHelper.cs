using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace GroundUp.infrastructure.utilities
{
    public static class ExpressionHelper
    {
        // Example Queries:
        // GET /api/inventory-categories?SortBy=Name
        // GET /api/inventory-categories?SortBy=-Name (Descending)
        public static IOrderedQueryable<T> ApplySorting<T>(IQueryable<T> query, string? sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return query.OrderBy(x => EF.Property<object>(x, "Id")); // Default sorting by Id
            }

            bool descending = sortBy.StartsWith("-");
            string propertyName = descending ? sortBy.Substring(1) : sortBy;

            var property = typeof(T).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            if (property == null)
            {
                return query.OrderBy(x => EF.Property<object>(x, "Id")); // Fallback if property is invalid
            }

            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);

            // Convert property to object for dynamic type handling
            var converted = Expression.Convert(propertyAccess, typeof(object));
            var lambda = Expression.Lambda<Func<T, object>>(converted, parameter);

            return descending ? query.OrderByDescending(lambda) : query.OrderBy(lambda);
        }

        // Example Query:
        // GET /api/inventory-categories?Filters[name]=cat
        public static Expression<Func<T, bool>> BuildPredicate<T>(PropertyInfo property, string filterValue)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);

            Expression comparison;
            if (property.PropertyType == typeof(string))
            {
                // Case-insensitive exact match for strings
                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                var lowerPropertyAccess = Expression.Call(propertyAccess, toLowerMethod);
                var lowerFilterValue = Expression.Constant(filterValue.ToLower(), typeof(string));

                comparison = Expression.Equal(lowerPropertyAccess, lowerFilterValue);
            }
            else
            {
                var constantValue = Expression.Constant(Convert.ChangeType(filterValue, property.PropertyType));
                comparison = Expression.Equal(propertyAccess, constantValue);
            }

            return Expression.Lambda<Func<T, bool>>(comparison, parameter);
        }

        // Example Query:
        // GET /api/inventory-categories?ContainsFilters[name]=Laptop
        public static Expression<Func<T, bool>> BuildContainsPredicate<T>(PropertyInfo property, string filterValue)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);

            if (property.PropertyType != typeof(string))
            {
                throw new ArgumentException($"Contains filter can only be applied to string properties. {property.Name} is {property.PropertyType}");
            }

            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            var lowerPropertyAccess = Expression.Call(propertyAccess, toLowerMethod);
            var lowerFilterValue = Expression.Constant(filterValue.ToLower(), typeof(string));

            var methodInfo = typeof(string).GetMethod("Contains", new[] { typeof(string) });

            var containsExpression = Expression.Call(lowerPropertyAccess, methodInfo, lowerFilterValue);
            return Expression.Lambda<Func<T, bool>>(containsExpression, parameter);
        }

        // Example Query:
        // GET /api/inventory-items?MinFilters[Price]=100&MaxFilters[Price]=500
        public static Expression<Func<T, bool>> BuildRangePredicate<T>(PropertyInfo property, string filterValue, bool isMin)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);
            var constantValue = Expression.Constant(Convert.ChangeType(filterValue, property.PropertyType));

            Expression comparison = isMin
                ? Expression.GreaterThanOrEqual(propertyAccess, constantValue) // Min: x.Property >= Value
                : Expression.LessThanOrEqual(propertyAccess, constantValue);  // Max: x.Property <= Value

            return Expression.Lambda<Func<T, bool>>(comparison, parameter);
        }

        // Example Queries:
        // GET /api/inventory-categories?MinFilters[CreatedDate]=2024-01-01
        // GET /api/inventory-categories?MaxFilters[CreatedDate]=2025-02-01
        // GET /api/inventory-categories?MinFilters[CreatedDate]=2024-01-01&MaxFilters[PurchaseDate]=2025-01-01
        public static Expression<Func<T, bool>> BuildDateRangePredicate<T>(PropertyInfo property, string filterValue, bool isMin)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);

            if (!DateTime.TryParse(filterValue, out DateTime dateValue))
            {
                throw new ArgumentException($"Invalid date format for {property.Name}: {filterValue}");
            }

            var constantValue = Expression.Constant(dateValue, typeof(DateTime));

            Expression comparison = isMin
                ? Expression.GreaterThanOrEqual(propertyAccess, constantValue) // x.PurchaseDate >= MinDate
                : Expression.LessThanOrEqual(propertyAccess, constantValue);  // x.PurchaseDate <= MaxDate

            return Expression.Lambda<Func<T, bool>>(comparison, parameter);
        }

        // Example Query:
        // GET /api/inventory-items?MultiValueFilters[Status]=Active,Pending
        public static Expression<Func<T, bool>> BuildMultiValuePredicate<T>(PropertyInfo property, string[] values)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);
            Type propertyType = property.PropertyType;

            var convertedValues = values
                .Select(value => Convert.ChangeType(value, propertyType))
                .ToList();

            var listType = typeof(List<>).MakeGenericType(propertyType);
            var typedValues = Activator.CreateInstance(listType);
            MethodInfo addMethod = listType.GetMethod("Add");
            foreach (var convertedValue in convertedValues)
            {
                addMethod.Invoke(typedValues, new object[] { convertedValue });
            }

            var containsMethod = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(propertyType);

            var containsCall = Expression.Call(containsMethod, Expression.Constant(typedValues), propertyAccess);

            return Expression.Lambda<Func<T, bool>>(containsCall, parameter);
        }
    }
}
