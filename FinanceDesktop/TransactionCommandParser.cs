using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceDesktop;

internal sealed record ParsedTransactionCommand(
    string Type,
    string Category,
    string Description,
    decimal Amount,
    DateTime EntryDate,
    string MonthKey,
    bool IsRecurring);

internal static class TransactionCommandParser
{
    private static readonly string[] MonthNames = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
        .Concat(CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames)
        .Where(name => !string.IsNullOrEmpty(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(name => name.Length)
        .ToArray();

    public static bool TryParse(
        string text,
        DateTime selectedMonth,
        DateTime today,
        IReadOnlyList<string> expenseCategories,
        IReadOnlyList<string> incomeCategories,
        IReadOnlyList<string> savingsCategories,
        out ParsedTransactionCommand? command,
        out string error)
    {
        command = null;
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Type a transaction, for example: “Spent ₹1,200 on transport today.”";
            return false;
        }

        var income = Regex.IsMatch(text, @"\b(salary|income|received|earned|paycheck)\b", RegexOptions.IgnoreCase);
        var savings = Regex.IsMatch(text, @"\b(invest(?:ed|ment)?|savings contribution|saved)\b", RegexOptions.IgnoreCase);
        if (income && savings)
        {
            error = "I couldn't tell whether this is income or a savings/investment contribution.";
            return false;
        }

        var type = savings ? "SAVINGS" : income ? "INCOME" : "EXPENSE";
        var categories = type switch
        {
            "INCOME" => incomeCategories,
            "SAVINGS" => savingsCategories,
            _ => expenseCategories
        };
        var category = FindCategory(text, categories);
        var dateText = RemoveDate(text, today, out var explicitDate, out var hasExplicitDate);
        var appliedMonthText = Regex.Replace(dateText,
            @"\b(?:applies\s+to|for)\s+(?:" + string.Join("|", MonthNames.Select(Regex.Escape)) + @")(?:\s+20\d{2})?\b",
            "", RegexOptions.IgnoreCase);
        var hasAppliedMonth = TryGetAppliedMonth(dateText, selectedMonth, out var explicitAppliedMonth);

        if (!TryReadAmount(appliedMonthText, out var amount))
        {
            error = "I couldn't find a valid amount. Include one number, such as ₹1,200.";
            return false;
        }

        if (category is null)
        {
            category = FindCategoryPhrase(text);
            if (string.IsNullOrWhiteSpace(category))
            {
                error = "I couldn't identify a category. Include one, for example: “on food” or “for transport.”";
                return false;
            }
        }

        DateTime entryDate;
        DateTime budgetMonth;
        var isSalary = type == "INCOME" && category.Equals("Salary", StringComparison.OrdinalIgnoreCase);
        if (isSalary && !hasExplicitDate)
        {
            budgetMonth = hasAppliedMonth ? explicitAppliedMonth : new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            entryDate = LastWeekdayOfMonth(budgetMonth.AddMonths(-1));
        }
        else
        {
            entryDate = hasExplicitDate ? explicitDate : DateForMonth(selectedMonth, today.Day);
            budgetMonth = hasAppliedMonth
                ? explicitAppliedMonth
                : isSalary
                    ? new DateTime(entryDate.Year, entryDate.Month, 1).AddMonths(1)
                    : hasExplicitDate
                        ? new DateTime(entryDate.Year, entryDate.Month, 1)
                        : new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
        }

        var recurring = isSalary || Regex.IsMatch(text,
            @"\b(recurring|monthly|every\s+month|each\s+month|repeat)\b", RegexOptions.IgnoreCase);
        command = new ParsedTransactionCommand(type, category, BuildDescription(text, category),
            amount, entryDate, budgetMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture), recurring);
        return true;
    }

    private static string? FindCategory(string text, IReadOnlyList<string> categories)
    {
        foreach (var category in categories.OrderByDescending(name => name.Length))
        {
            if (Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(category)}(?![\p{{L}}\p{{N}}])",
                    RegexOptions.IgnoreCase))
                return category;
        }
        return null;
    }

    private static string? FindCategoryPhrase(string text)
    {
        var match = Regex.Match(text,
            @"\b(?:on|for|in|category)\s+(?<category>[a-z][a-z &-]{0,30})",
            RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var phrase = Regex.Replace(match.Groups["category"].Value,
            @"\b(today|yesterday|monthly|recurring|every|each|month|on|for|at|date|to)\b.*$",
            "", RegexOptions.IgnoreCase).Trim(' ', ',', '.', ';', ':', '-');
        return string.IsNullOrWhiteSpace(phrase) ? null : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(phrase);
    }

    private static string BuildDescription(string text, string category)
    {
        var description = Regex.Replace(text, @"(?:₹|INR)\s*[\d,]+(?:\.\d{1,2})?|\b[\d,]+(?:\.\d{1,2})?\s*(?:₹|INR)?",
            "", RegexOptions.IgnoreCase);
        description = Regex.Replace(description,
            @"\b(?:i|have|had|spent|spend|paid|pay|bought|buy|expense|income|received|earned|salary|paycheck|invested|invest|saving|savings|saved|on|for|in|today|yesterday|monthly|recurring|every|each|month|applies|to|date|category|extra|more|another)\b",
            "", RegexOptions.IgnoreCase);
        description = Regex.Replace(description, @"\b(?:" + string.Join("|", MonthNames.Select(Regex.Escape)) + @")\b",
            "", RegexOptions.IgnoreCase);
        description = Regex.Replace(description, @"\b20\d{2}\b|\b\d{1,2}(?:st|nd|rd|th)?\b", "");
        description = Regex.Replace(description, Regex.Escape(category), "", RegexOptions.IgnoreCase);
        description = Regex.Replace(description, @"[\p{P}\p{S}]+", " ");
        description = Regex.Replace(description, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(description) ? category : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(description);
    }

    private static string RemoveDate(string text, DateTime today, out DateTime date, out bool hasDate)
    {
        date = today.Date;
        hasDate = false;
        var result = text;
        var relativeDate = Regex.Match(result, @"\b(today|yesterday)\b", RegexOptions.IgnoreCase);
        if (relativeDate.Success)
        {
            date = relativeDate.Value.Equals("yesterday", StringComparison.OrdinalIgnoreCase)
                ? today.Date.AddDays(-1)
                : today.Date;
            hasDate = true;
            result = result.Remove(relativeDate.Index, relativeDate.Length);
        }

        var iso = Regex.Match(result, @"\b(?<date>20\d{2}-\d{1,2}-\d{1,2})\b");
        if (iso.Success && DateTime.TryParseExact(iso.Groups["date"].Value, ["yyyy-M-d", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedIso))
        {
            date = parsedIso.Date;
            hasDate = true;
            result = result.Remove(iso.Index, iso.Length);
        }

        var numeric = Regex.Match(result, @"\b(?<date>\d{1,2}/\d{1,2}/20\d{2})\b");
        if (numeric.Success && DateTime.TryParseExact(numeric.Groups["date"].Value, "d/M/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedNumeric))
        {
            date = parsedNumeric.Date;
            hasDate = true;
            result = result.Remove(numeric.Index, numeric.Length);
        }

        var namedDate = Regex.Match(result,
            @"\b(?:(?<day>\d{1,2})(?:st|nd|rd|th)?\s+(?<month>[a-z]{3,9})|(?<month2>[a-z]{3,9})\s+(?<day2>\d{1,2})(?:st|nd|rd|th)?)(?:,?\s+(?<year>20\d{2}))?\b",
            RegexOptions.IgnoreCase);
        if (namedDate.Success)
        {
            var monthName = namedDate.Groups["month"].Success
                ? namedDate.Groups["month"].Value
                : namedDate.Groups["month2"].Value;
            var day = int.Parse(namedDate.Groups["day"].Success
                ? namedDate.Groups["day"].Value
                : namedDate.Groups["day2"].Value, CultureInfo.InvariantCulture);
            var year = namedDate.Groups["year"].Success
                ? int.Parse(namedDate.Groups["year"].Value, CultureInfo.InvariantCulture)
                : today.Year;
            if (TryGetMonthNumber(monthName, out var monthNumber) &&
                day >= 1 && day <= DateTime.DaysInMonth(year, monthNumber))
            {
                date = new DateTime(year, monthNumber, day);
                hasDate = true;
                result = result.Remove(namedDate.Index, namedDate.Length);
            }
        }
        return result;
    }

    private static bool TryGetAppliedMonth(string text, DateTime selectedMonth, out DateTime month)
    {
        var match = Regex.Match(text,
            @"\b(?:applies\s+to|for)\s+(?<month>" + string.Join("|", MonthNames.Select(Regex.Escape)) +
            @")(?:\s+(?<year>20\d{2}))?\b", RegexOptions.IgnoreCase);
        if (!match.Success || !TryGetMonthNumber(match.Groups["month"].Value, out var monthNumber))
        {
            month = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            return false;
        }

        var year = match.Groups["year"].Success
            ? int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture)
            : selectedMonth.Year;
        month = new DateTime(year, monthNumber, 1);
        return true;
    }

    private static bool TryGetMonthNumber(string name, out int month)
    {
        for (var index = 1; index <= 12; index++)
        {
            if (name.Equals(CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(index), StringComparison.OrdinalIgnoreCase) ||
                name.Equals(CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(index), StringComparison.OrdinalIgnoreCase))
            {
                month = index;
                return true;
            }
        }
        month = 0;
        return false;
    }

    private static bool TryReadAmount(string text, out decimal amount)
    {
        amount = 0;
        var currencyAmount = Regex.Match(text,
            @"(?:₹|INR)\s*(?<amount>\d[\d,]*(?:\.\d{1,2})?)|(?<amount>\d[\d,]*(?:\.\d{1,2})?)\s*(?:₹|INR)",
            RegexOptions.IgnoreCase);
        var match = currencyAmount.Success
            ? currencyAmount
            : Regex.Match(text, @"(?<![\w/])(?<amount>\d[\d,]*(?:\.\d{1,2})?)(?![\w/])");
        return match.Success && decimal.TryParse(match.Groups["amount"].Value.Replace(",", ""),
            NumberStyles.Number, CultureInfo.InvariantCulture, out amount) && amount > 0;
    }

    private static DateTime DateForMonth(DateTime month, int requestedDay) =>
        new(month.Year, month.Month, Math.Min(requestedDay, DateTime.DaysInMonth(month.Year, month.Month)));

    private static DateTime LastWeekdayOfMonth(DateTime month)
    {
        var date = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(-1);
        return date;
    }
}
