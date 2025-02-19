using System.Linq.Expressions;
using System.Reflection;

namespace GroundUp.infrastructure.utilities
{
    public static class ExpressionHelper
    {
        public static Expression<Func<T, bool>> BuildPredicate<T>(PropertyInfo property, string filterValue)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);
            var constantValue = Expression.Constant(Convert.ChangeType(filterValue, property.PropertyType));
            var equality = Expression.Equal(propertyAccess, constantValue);
            return Expression.Lambda<Func<T, bool>>(equality, parameter);
        }
    }
}
