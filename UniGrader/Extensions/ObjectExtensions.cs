using System.Reflection;
using System.Text;

namespace UniGrader.Extensions;

public static class ObjectExtensions
{
    
    public static string ToTable<T>(this IEnumerable<T> items,
        string[] columnHeaders,
        params Func<T, object>[] selectors)
    {
        if (columnHeaders.Length != selectors.Length)
            throw new Exception("ToTable requires that column headers and selectors be the same length");

        var values = new string[items.Count() + 1, selectors.Length];
        
        // Create the headers
        for (int colIndex = 0; colIndex < values.GetLength(1); colIndex++)
            values[0, colIndex] = columnHeaders[colIndex];
        
        // Fill the table with data
        for (int rowIndex = 1; rowIndex < values.GetLength(0); rowIndex++)
        {
            for (int colIndex = 0; colIndex < values.GetLength(1); colIndex++)
            {
                values[rowIndex, colIndex] = selectors[colIndex]
                    .Invoke(items.ElementAt(rowIndex-1))
                    .ToString();
            }
        }

        return ToStringTable(values);
    }

    public static string ToTable<T>(this T obj, params (string header, Func<T, object> value)[] selectors)
    {
        var values = new string[2, selectors.Length];
        
        // create the headers
        for (int colIndex = 0; colIndex < selectors.Length; colIndex++)
        {
            values[0, colIndex] = selectors[colIndex].header;
            values[1, colIndex] = selectors[colIndex].value.Invoke(obj).ToString() ?? "-";
        }

        return ToStringTable(values);
    }

    static string ToStringTable(this string[,] values)
    {
        int[] maxColumnsWidth = GetMaxColumnWidth(values);
        var headerSplitter = new string('-', maxColumnsWidth
            .Sum(i => i + 3) - 1);

        var builder = new StringBuilder();

        for (int rowIndex = 0; rowIndex < values.GetLength(0); rowIndex++)
        {
            for (int colIndex = 0; colIndex < values.GetLength(1); colIndex++)
            {
                // Print Cell
                string cell = values[rowIndex, colIndex];

                cell = cell.PadRight(maxColumnsWidth[colIndex]);
                builder.Append(" | ");
                builder.Append(cell);
            }

            builder.Append(" | ");
            builder.AppendLine();

            if (rowIndex == 0)
            {
                builder.AppendFormat(" |{0}| ", headerSplitter);
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
    
    static int[] GetMaxColumnWidth(string[,] values)
    {
        var maxColumnWidth = new int[values.GetLength(1)];

        for (int colIndex = 0; colIndex < values.GetLength(1); colIndex++)
        {
            for (int rowIndex = 0; rowIndex < values.GetLength(0); rowIndex++)
            {
                int newLength = values[rowIndex, colIndex].Length;
                int oldLength = maxColumnWidth[colIndex];

                if (newLength > oldLength)
                    maxColumnWidth[colIndex] = newLength;
            }
        }
        
        return maxColumnWidth;
    }
}