// Shared client-side table behavior for the Dealer Dashboard's lightweight
// watchlist-style tables (Views/DealerDashboard/Index.cshtml and the report
// pages under Views/DealerDashboardReports/): "Show more/less" paging and
// click-to-sort column headers. Fully generic -- driven by data-key/
// data-page-size/data-watchlist-row attributes on the markup, no per-page
// logic here.
window.Ranalo = window.Ranalo || {};

function updateWatchlistControls(key, pageSize, visibleCount, total) {
    var moreLink = document.querySelector('a[data-more-for="' + key + '"]');
    var lessLink = document.querySelector('a[data-less-for="' + key + '"]');
    var remaining = total - visibleCount;
    if (moreLink) {
        if (remaining > 0) {
            moreLink.style.display = '';
            moreLink.textContent = 'Show ' + Math.min(pageSize, remaining) + ' more';
        } else {
            moreLink.style.display = 'none';
        }
    }
    if (lessLink) {
        lessLink.style.display = visibleCount > pageSize ? '' : 'none';
    }
}

// "Show N more" keeps adding another page of rows each click, however many
// times there are more to reveal.
window.Ranalo.showMoreRows = function (link, key, pageSize) {
    var rows = [].slice.call(document.querySelectorAll('tr[data-watchlist-row="' + key + '"]'));
    var hidden = rows.filter(function (row) { return row.classList.contains('d-none'); });
    hidden.slice(0, pageSize).forEach(function (row) { row.classList.remove('d-none'); });

    var visibleCount = rows.length - (hidden.length - Math.min(pageSize, hidden.length));
    updateWatchlistControls(key, pageSize, visibleCount, rows.length);
};

// "Show less" is available as soon as more than the initial page is showing,
// and always collapses straight back to the first `pageSize` rows regardless
// of how many are currently expanded.
window.Ranalo.showLessRows = function (link, key, pageSize) {
    var rows = [].slice.call(document.querySelectorAll('tr[data-watchlist-row="' + key + '"]'));
    rows.forEach(function (row, i) { row.classList.toggle('d-none', i >= pageSize); });
    updateWatchlistControls(key, pageSize, pageSize, rows.length);
};

// Click a column header to sort its table by that column; click again to
// reverse direction. Numeric-looking columns (KES amounts, %, "45 days",
// ranks) sort numerically; everything else sorts as text.
window.Ranalo.sortTable = function (th) {
    var table = th.closest('table');
    var key = table.getAttribute('data-key');
    var pageSize = parseInt(table.getAttribute('data-page-size'), 10) || 5;
    var headerRow = th.parentElement;
    var colIndex = Array.prototype.indexOf.call(headerRow.children, th);
    var tbody = table.querySelector('tbody');
    var rows = Array.prototype.slice.call(tbody.querySelectorAll('tr'));

    // Sorting must preserve however many rows are currently expanded, not
    // silently collapse or fully expand the table.
    var visibleCount = rows.filter(function (row) { return !row.classList.contains('d-none'); }).length;

    var dir = th.getAttribute('data-sort-dir') === 'asc' ? 'desc' : 'asc';
    Array.prototype.forEach.call(headerRow.children, function (cell) {
        cell.removeAttribute('data-sort-dir');
        var indicator = cell.querySelector('.wl-sort-indicator');
        if (indicator) indicator.remove();
    });
    th.setAttribute('data-sort-dir', dir);
    var arrow = document.createElement('span');
    arrow.className = 'wl-sort-indicator';
    arrow.style.marginLeft = '4px';
    arrow.textContent = dir === 'asc' ? '▲' : '▼';
    th.appendChild(arrow);

    function cellText(row) {
        var cell = row.children[colIndex];
        return cell ? cell.textContent.trim() : '';
    }
    function numericValue(text) {
        var cleaned = text.replace(/[^0-9.\-]/g, '');
        return cleaned === '' || cleaned === '-' ? NaN : parseFloat(cleaned);
    }

    var allNumeric = rows.every(function (row) { return !isNaN(numericValue(cellText(row))); });
    rows.sort(function (a, b) {
        var cmp;
        if (allNumeric) {
            cmp = numericValue(cellText(a)) - numericValue(cellText(b));
        } else {
            cmp = cellText(a).localeCompare(cellText(b), undefined, { sensitivity: 'base' });
        }
        return dir === 'asc' ? cmp : -cmp;
    });

    rows.forEach(function (row, i) {
        row.classList.toggle('d-none', i >= visibleCount);
        tbody.appendChild(row);
    });

    updateWatchlistControls(key, pageSize, visibleCount, rows.length);
};
