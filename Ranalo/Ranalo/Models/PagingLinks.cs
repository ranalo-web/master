using Microsoft.AspNetCore.Http;

namespace Ranalo.Models
{
    // Page links (Prev/Next/numbers/"Page x of y") must keep the search and
    // any other filters in the address, or page 2 of a search shows page 2
    // of everything (2026-11-20).
    public static class PagingLinks
    {
        // "&searchTerm=...&..." for every query value except the ones the
        // link sets itself (page, and pageSize when the link has it).
        public static string Keep(HttpRequest request, params string[] except)
        {
            var skip = new HashSet<string>(except.Append("page"), StringComparer.OrdinalIgnoreCase);
            var parts = request.Query
                .Where(q => !skip.Contains(q.Key))
                .SelectMany(q => q.Value.Where(v => !string.IsNullOrEmpty(v))
                    .Select(v => Uri.EscapeDataString(q.Key) + "=" + Uri.EscapeDataString(v!)));
            return string.Concat(parts.Select(p => "&" + p));
        }
    }
}
