namespace Ranalo.UiHelpers
{
    public static class PaginationExtensions
    {
        public static IEnumerable<T> Paginate<T>(this IEnumerable<T> source, int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;  // Ensure at least page 1
            if (pageSize < 1) pageSize = 10;     // Default page size

            return source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);
        }
    }
}
