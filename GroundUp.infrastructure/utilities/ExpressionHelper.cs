using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace GroundUp.infrastructure.utilities
{
    public static class ExpressionHelper
    {
        // Apply Sorting with Default Fallback
        // GET /api/inventory-items?SortBy=PurchaseDate
        // GET /api/inventory-items?SortBy=-PurchaseDate
        public static IOrderedQueryable<T> ApplySorting<T>(IQueryable<T> query, string? sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return query.OrderBy(x => EF.Property<object>(x, "Id")); // Default sorting by Id
            }

            bool descending = sortBy.StartsWith("-");
            string propertyName = descending ? sortBy.Substring(1) : sortBy;

            var property = typeof(T).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
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

        // Build Predicate for Exact Matching
        // GET /api/inventory-items?Filters[Condition]=New
        public static Expression<Func<T, bool>> BuildPredicate<T>(PropertyInfo property, string filterValue)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);
            var constantValue = Expression.Constant(Convert.ChangeType(filterValue, property.PropertyType));
            var equality = Expression.Equal(propertyAccess, constantValue);
            return Expression.Lambda<Func<T, bool>>(equality, parameter);
        }

        // Build Predicate for Range Filtering
        // GET /api/inventory-items?MinFilters[PurchasePrice]=100&MaxFilters[PurchasePrice]=500
        public static Expression<Func<T, bool>> BuildRangePredicate<T>(PropertyInfo property, string filterValue, bool isMin)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);
            var constantValue = Expression.Constant(Convert.ChangeType(filterValue, property.PropertyType));

            Expression comparison = isMin
                ? Expression.GreaterThanOrEqual(propertyAccess, constantValue) // Min: x.Property >= Value
                : Expression.LessThanOrEqual(propertyAccess, constantValue); // Max: x.Property <= Value

            return Expression.Lambda<Func<T, bool>>(comparison, parameter);
        }

        // Build Predicate for Date Range Filtering
        // GET /api/inventory-items?MinFilters[PurchaseDate]=2024-01-01&MaxFilters[PurchaseDate]=2025-01-01
        // GET /api/inventory-items?MinFilters[PurchaseDate]=2024-01-01
        // GET /api/inventory-items?MaxFilters[PurchaseDate]=2025-02-01
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

        // Build Predicate for Multiple Values Filtering (IN Operator)
        // GET /api/inventory-items?MultiValueFilters[Condition]=New,Used
        public static Expression<Func<T, bool>> BuildMultiValuePredicate<T>(PropertyInfo property, string[] values)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);
            Type propertyType = property.PropertyType;

            // 🔹 Convert each value to match the property type
            var convertedValues = values
                .Select(value => Convert.ChangeType(value, propertyType))
                .ToList(); // Ensure List<T> format

            // 🔹 Create a strongly-typed constant list
            var listType = typeof(List<>).MakeGenericType(propertyType);
            var typedValues = Activator.CreateInstance(listType);
            MethodInfo addMethod = listType.GetMethod("Add");
            foreach (var convertedValue in convertedValues)
            {
                addMethod.Invoke(typedValues, new object[] { convertedValue });
            }

            // 🔹 Get Contains<T>(IEnumerable<T>, T) method
            var containsMethod = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(propertyType);

            // 🔹 Build Contains expression: typedValues.Contains(propertyAccess)
            var containsCall = Expression.Call(containsMethod, Expression.Constant(typedValues), propertyAccess);

            return Expression.Lambda<Func<T, bool>>(containsCall, parameter);
        }

        // Builds "Contains" Predicate for Search
        // api/inventory-items?SearchTerm=laptop
        public static Expression<Func<T, bool>> BuildSearchPredicate<T>(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return x => true; // No filtering applied if search is empty

            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? combinedExpression = null;

            // Get all string properties dynamically
            var stringProperties = typeof(T).GetProperties()
                .Where(p => p.PropertyType == typeof(string))
                .ToList();

            foreach (var property in stringProperties)
            {
                var propertyAccess = Expression.Property(parameter, property);

                var methodInfo = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                var searchValue = Expression.Constant(searchTerm, typeof(string));

                if (methodInfo != null)
                {
                    var containsExpression = Expression.Call(propertyAccess, methodInfo, searchValue);
                    combinedExpression = combinedExpression == null
                        ? containsExpression
                        : Expression.OrElse(combinedExpression, containsExpression);
                }
            }

            return combinedExpression != null
                ? Expression.Lambda<Func<T, bool>>(combinedExpression, parameter)
                : x => true;
        }
    }
}
